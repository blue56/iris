using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Iris.Persistence;

/// <summary>
/// Background service that periodically cleans up old completed messages from the message store.
/// Works with any message type that implements IPersistedMessage.
/// </summary>
/// <typeparam name="TMessage">The message type being cleaned up.</typeparam>
public sealed class MessageCleanupService<TMessage> : BackgroundService 
    where TMessage : class, IPersistedMessage
{
    private readonly IMessageStore<TMessage> _messageStore;
    private readonly MessageStoreOptions _options;
    private readonly ILogger<MessageCleanupService<TMessage>> _logger;

    public MessageCleanupService(
        IMessageStore<TMessage> messageStore,
        MessageStoreOptions options,
        ILogger<MessageCleanupService<TMessage>> logger)
    {
        _messageStore = messageStore;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.Maintenance.EnableAutoCleanup)
        {
            _logger.LogInformation("Message store cleanup is disabled");
            return;
        }

        _logger.LogInformation("Message store cleanup service starting. Interval: {Interval} minutes, Retention: {Retention} hours",
            _options.Maintenance.CleanupIntervalMinutes,
            _options.Deduplication.RetentionHours);

        var interval = TimeSpan.FromMinutes(_options.Maintenance.CleanupIntervalMinutes);
        var retention = TimeSpan.FromHours(_options.Deduplication.RetentionHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                _logger.LogDebug("Starting message store cleanup...");
                var deletedCount = await _messageStore.CleanupOldMessagesAsync(retention, stoppingToken);

                if (deletedCount > 0)
                {
                    var stats = await _messageStore.GetStatsAsync(stoppingToken);
                    _logger.LogInformation(
                        "Cleanup completed. Deleted: {Deleted}, Remaining - Pending: {Pending}, Completed: {Completed}, Failed: {Failed}, DB Size: {Size:N0} bytes",
                        deletedCount, stats.PendingCount, stats.CompletedCount, stats.FailedCount, stats.DatabaseSizeBytes);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during message store cleanup");
            }
        }

        _logger.LogInformation("Message store cleanup service stopped");
    }
}
