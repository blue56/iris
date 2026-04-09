using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Iris.Core;
using Iris.Core.Plugins;
using Iris.Persistence;
using Iris.Plugins.Configuration;
using Iris.Plugins.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Iris.Plugins.Targets;

/// <summary>
/// A unified MQTT transport that can both publish and subscribe to MQTT topics.
/// </summary>
[Plugin("Mqtt", "1.0.0", PluginType.Transport,
    Author = "Iris Team",
    Description = "Publishes and subscribes to messages from an MQTT broker")]
public sealed class MqttTransport : ITransport, IDisposable
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttTransport> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private IMessageStore<MqttMessage>? _messageStore;
    private MqttMessageQueueClient? _mqttSenderClient;
    private MqttMessageQueueClient? _mqttReceiverClient;
    private CancellationTokenSource? _cts;

    public string Name => _options.Name;
    public event Func<DataMessage, Task>? MessageReceived;

    public MqttTransport(MqttOptions options, ILogger<MqttTransport> logger, ILoggerFactory loggerFactory)
    {
        _options = options;
        _logger = logger;
        _loggerFactory = loggerFactory;

        if (_options.MessageStore?.Enabled == true)
        {
            _messageStore = new SqliteMessageStore(_options.MessageStore, loggerFactory);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || string.IsNullOrWhiteSpace(_options.SubscribeTopic))
        {
            return; // Not configured as a listener
        }

        if (string.IsNullOrWhiteSpace(_options.BrokerHost))
        {
            _logger.LogWarning("Cannot listen to MQTT - BrokerHost is not configured.");
            return;
        }

        _logger.LogInformation("MqttTransport starting listener on {Host}:{Port}, topic: {Topic}", 
            _options.BrokerHost, _options.BrokerPort, _options.SubscribeTopic);

        await ConnectReceiverAsync(cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await _mqttReceiverClient!.SubscribeAsync(OnMqttMessageReceived, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_cts != null)
        {
            _cts.Cancel();
        }

        await Task.CompletedTask;
    }

    public async Task SendAsync(DataMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BrokerHost))
        {
            _logger.LogWarning("Cannot send message {Id} - MQTT BrokerHost is not configured.", message.Id);
            return;
        }

        await ConnectSenderAsync(cancellationToken);

        await _mqttSenderClient!.PublishAsync(message, cancellationToken);
        _logger.LogInformation("Message {Id} sent to MQTT topic {Topic}.", message.Id, _options.Topic);
    }

    private async Task ConnectSenderAsync(CancellationToken cancellationToken)
    {
        if (_mqttSenderClient == null)
        {
            _mqttSenderClient = new MqttMessageQueueClient(
                _options.BrokerHost,
                _options.BrokerPort,
                _options.Topic,
                _options.Username,
                _options.Password,
                _loggerFactory.CreateLogger<MqttMessageQueueClient>());

            await _mqttSenderClient.ConnectAsync(cancellationToken);
        }
    }

    private async Task ConnectReceiverAsync(CancellationToken cancellationToken)
    {
        if (_mqttReceiverClient == null)
        {
            _mqttReceiverClient = new MqttMessageQueueClient(
                _options.BrokerHost,
                _options.BrokerPort,
                _options.SubscribeTopic,
                _options.Username,
                _options.Password,
                _loggerFactory.CreateLogger<MqttMessageQueueClient>());

            await _mqttReceiverClient.ConnectAsync(cancellationToken);
        }
    }

    private async Task OnMqttMessageReceived(DataMessage message)
    {
        if (MessageReceived != null)
        {
            // Just invoke without the full dedup store for brevity unless requested.
            // Simplified handling for unified transport.
            try
            {
                await MessageReceived(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to route message from MQTT listener.");
            }
        }
    }

    public void Dispose()
    {
        _mqttSenderClient?.Dispose();
        _mqttReceiverClient?.Dispose();
        _cts?.Dispose();
    }
}