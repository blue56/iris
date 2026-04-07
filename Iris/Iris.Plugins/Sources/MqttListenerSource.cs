using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Iris.Core;
using Iris.Core.Plugins;
using Iris.Plugins.Configuration;
using Iris.Plugins.Messaging;
using Iris.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Iris.Plugins.Sources;

/// <summary>
/// Subscribes to an MQTT topic for messages and raises <see cref="MessageReceived"/>
/// for each one.
/// </summary>
[Plugin("MqttListener", "1.0.0", PluginType.Source,
    Author = "Iris Team",
    Description = "Subscribes to MQTT topics and receives messages")]
public sealed class MqttListenerSource : ISource, IDisposable
{
    private readonly MqttListenerOptions _options;
    private readonly ILogger<MqttListenerSource> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private IMessageStore<MqttMessage>? _messageStore;
    private MqttMessageQueueClient? _mqttClient;
    private CancellationTokenSource? _cts;
    private long _duplicateCount;
    private long _processedCount;

    public event Func<DataMessage, Task>? MessageReceived;
    public IReadOnlyList<string> TargetNames => _options.Targets;

    public MqttListenerSource(
        IConfiguration configuration,
        ILogger<MqttListenerSource> logger,
        ILoggerFactory loggerFactory)
    {
        _options = configuration.GetSection("Sources:MqttListener").Get<MqttListenerOptions>() ?? new MqttListenerOptions();
        _logger = logger;
        _loggerFactory = loggerFactory;

        // Create message store if enabled
        if (_options.MessageStore?.Enabled == true)
        {
            _messageStore = new SqliteMessageStore(_options.MessageStore, loggerFactory);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("MqttListenerSource is disabled.");
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.BrokerHost))
        {
            _logger.LogWarning("MqttListenerSource is enabled but BrokerHost is not configured. Skipping.");
            return;
        }

        // Initialize message store if enabled
        if (_messageStore != null && _options.MessageStore?.Enabled == true)
        {
            await _messageStore.InitializeAsync(cancellationToken);

            // Replay pending messages from buffer if configured
            if (_options.MessageStore.Buffering.ReplayOnStartup)
            {
                await ReplayPendingMessagesAsync(cancellationToken);
            }

            // Log initial stats
            var stats = await _messageStore.GetStatsAsync(cancellationToken);
            _logger.LogInformation(
                "Message store ready. Pending: {Pending}, Completed: {Completed}, Failed: {Failed}, DB Size: {Size:N0} bytes",
                stats.PendingCount, stats.CompletedCount, stats.FailedCount, stats.DatabaseSizeBytes);
        }

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _mqttClient = new MqttMessageQueueClient(
            _options.BrokerHost,
            _options.BrokerPort,
            _options.Topic,
            _options.Username,
            _options.Password,
            _loggerFactory.CreateLogger<MqttMessageQueueClient>());

        await _mqttClient.ConnectAsync(_cts.Token);
        await _mqttClient.SubscribeAsync(HandleMessageAsync, _cts.Token);

        _logger.LogInformation("MqttListenerSource subscribed to topic {Topic}.", _options.Topic);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync();

        // Log final statistics if message store is enabled
        if (_messageStore != null && _options.MessageStore?.Enabled == true)
        {
            var stats = await _messageStore.GetStatsAsync(cancellationToken);
            _logger.LogInformation(
                "MqttListenerSource stopping. Stats - Processed: {Processed}, Duplicates: {Duplicates}, " +
                "Pending: {Pending}, Completed: {Completed}, Failed: {Failed}",
                _processedCount, _duplicateCount, stats.PendingCount, stats.CompletedCount, stats.FailedCount);
        }

        _logger.LogInformation("MqttListenerSource stopped.");
    }

    private async Task ReplayPendingMessagesAsync(CancellationToken cancellationToken)
    {
        var pending = await _messageStore!.GetPendingMessagesAsync(_options.MessageStore!.Buffering.MaxBacklogSize, cancellationToken);

        if (pending.Count == 0)
        {
            _logger.LogInformation("No pending messages to replay");
            return;
        }

        _logger.LogInformation("Replaying {Count} pending messages from buffer", pending.Count);

        foreach (var mqttMsg in pending)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Check if message exceeded retry limit
            if (mqttMsg.AttemptCount >= _options.MessageStore.Buffering.MaxRetryAttempts)
            {
                _logger.LogWarning(
                    "Message {MessageId} exceeded retry limit ({Attempts}/{Max}), marking as permanently failed",
                    mqttMsg.MessageId, mqttMsg.AttemptCount, _options.MessageStore.Buffering.MaxRetryAttempts);

                await _messageStore.MarkAsFailedAsync(
                    mqttMsg.MessageId, 
                    "Maximum retry attempts exceeded", 
                    mqttMsg.AttemptCount, 
                    cancellationToken);
                continue;
            }

            try
            {
                await _messageStore.MarkAsProcessingAsync(mqttMsg.MessageId, cancellationToken);

                // Reconstruct DataMessage from stored MQTT message
                var metadata = string.IsNullOrEmpty(mqttMsg.MetadataJson)
                    ? new Dictionary<string, string>()
                    : JsonSerializer.Deserialize<Dictionary<string, string>>(mqttMsg.MetadataJson) ?? new Dictionary<string, string>();

                metadata["Replayed"] = "true";
                metadata["OriginalReceivedAt"] = mqttMsg.ReceivedAt.ToString("O");
                metadata["AttemptCount"] = mqttMsg.AttemptCount.ToString();

                var dataMessage = new DataMessage
                {
                    Body = mqttMsg.Payload,
                    Metadata = metadata
                };

                _logger.LogInformation(
                    "Replaying message {MessageId} from buffer (attempt {Attempt}/{Max})",
                    mqttMsg.MessageId, mqttMsg.AttemptCount + 1, _options.MessageStore.Buffering.MaxRetryAttempts);

                // Process through sinks
                if (MessageReceived is not null)
                    await MessageReceived(dataMessage);

                // Mark as completed on success
                await _messageStore.MarkAsCompletedAsync(mqttMsg.MessageId, cancellationToken);
                _logger.LogInformation("Replayed message {MessageId} delivered successfully", mqttMsg.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to replay message {MessageId} (attempt {Attempt})", 
                    mqttMsg.MessageId, mqttMsg.AttemptCount + 1);

                await _messageStore.MarkAsFailedAsync(
                    mqttMsg.MessageId, 
                    ex.Message, 
                    mqttMsg.AttemptCount + 1, 
                    cancellationToken);
            }

            // Add small delay between replays to avoid overwhelming sinks
            if (_options.MessageStore.Buffering.RetryDelaySeconds > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.MessageStore.Buffering.RetryDelaySeconds), cancellationToken);
            }
        }
    }

    private async Task HandleMessageAsync(DataMessage message)
    {
        // If message store is enabled, use it for deduplication and buffering
        if (_messageStore != null && _options.MessageStore?.Enabled == true)
        {
            await HandleMessageWithStoreAsync(message);
        }
        else
        {
            // Original behavior without persistence
            await HandleMessageDirectAsync(message);
        }
    }

    private async Task HandleMessageWithStoreAsync(DataMessage message)
    {
        try
        {
            var topic = message.Metadata.GetValueOrDefault("MqttTopic", _options.Topic);

            // Generate application-level message ID for deduplication
            // Note: MQTT protocol doesn't define message IDs, only Packet Identifiers (session-scoped)
            // This uses content hashing or MQTT 5.0 Correlation Data for cross-session deduplication
            var messageId = MessageIdExtractor.GetMessageId(
                message.Body,
                topic,
                message.Metadata);

            // Get MQTT protocol metadata for logging (DUP flag, Packet ID, QoS)
            var (isMqttDup, packetId, qos) = MessageIdExtractor.GetMqttProtocolInfo(message.Metadata);

            if (isMqttDup)
            {
                _logger.LogWarning(
                    "MQTT protocol duplicate flag detected. Topic: {Topic}, QoS: {QoS}, PacketId: {PacketId}, MessageId: {MessageId}",
                    topic, qos, packetId, messageId);
            }

            // Create MQTT message for storage
            var mqttMessage = new MqttMessage
            {
                MessageId = messageId,
                Topic = topic,
                Payload = message.Body,
                ReceivedAt = DateTimeOffset.UtcNow,
                Status = MessageStatus.Pending,
                AttemptCount = 0,
                MetadataJson = JsonSerializer.Serialize(message.Metadata)
            };

            // Atomic deduplication check + store
            bool isNew = await _messageStore!.TryStoreNewMessageAsync(mqttMessage, CancellationToken.None);

            if (!isNew)
            {
                Interlocked.Increment(ref _duplicateCount);
                _logger.LogWarning(
                    "Duplicate MQTT message detected and skipped. MessageId: {MessageId}, Topic: {Topic}, QoS: {QoS}, PacketId: {PacketId}",
                    messageId, topic, qos, packetId ?? "N/A");
                return;
            }

            _logger.LogInformation(
                "New MQTT message stored. MessageId: {MessageId}, Topic: {Topic}, QoS: {QoS}, PacketId: {PacketId}, MqttDup: {MqttDup}", 
                messageId, topic, qos, packetId ?? "N/A", isMqttDup);

            try
            {
                // Mark as processing
                await _messageStore.MarkAsProcessingAsync(messageId, CancellationToken.None);

                // Process through pipeline
                if (MessageReceived is not null)
                    await MessageReceived(message);

                // Mark as completed on success
                await _messageStore.MarkAsCompletedAsync(messageId, CancellationToken.None);
                Interlocked.Increment(ref _processedCount);

                _logger.LogInformation(
                    "Message delivered successfully. MessageId: {MessageId}, Topic: {Topic}, QoS: {QoS}", 
                    messageId, topic, qos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {MessageId} (attempt 1)", messageId);

                // Mark as failed for retry
                await _messageStore.MarkAsFailedAsync(messageId, ex.Message, 1, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            // Catch-all for any errors during message ID extraction or storage
            _logger.LogError(ex, 
                "Failed to process MQTT message with store. Topic: {Topic}, PayloadLength: {Length}, PayloadPreview: {Preview}",
                message.Metadata.GetValueOrDefault("MqttTopic", _options.Topic),
                message.Body?.Length ?? 0,
                message.Body?.Length > 100 ? message.Body[..100] : message.Body);

            // Fall back to processing without store to avoid losing the message
            try
            {
                _logger.LogWarning("Falling back to direct processing without store for this message");
                await HandleMessageDirectAsync(message);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Fallback processing also failed");
            }
        }
    }

    private async Task HandleMessageDirectAsync(DataMessage message)
    {
        _logger.LogInformation("Received MQTT message {MessageId}.", message.Id);

        if (MessageReceived is not null)
            await MessageReceived(message);
    }

    public void Dispose()
    {
        _mqttClient?.Dispose();
        _cts?.Dispose();
    }
}
