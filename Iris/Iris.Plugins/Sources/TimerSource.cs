using Iris.Core;
using Iris.Core.Plugins;
using Microsoft.Extensions.Logging;

namespace Iris.Plugins.Sources;

/// <summary>
/// Example plugin that generates test messages on a timer.
/// Useful for testing and demonstration purposes.
/// </summary>
[Plugin("TimerSource", "1.0.0", PluginType.Connector,
    Author = "Iris Plugins",
    Description = "Generates test messages on a configurable timer interval")]
public sealed class TimerSource : IConnector, IDisposable
{
    private readonly ILogger<TimerSource> _logger;
    private readonly TimeSpan _interval;
    private readonly ITransport? _transport;
    private Timer? _timer;
    private int _messageCount;

    public event Func<DataMessage, Task>? MessageReceived;
    public ITransport? Transport => _transport;
    public string Name { get; } = "timerSource";

    /// <summary>
    /// Creates a timer source that generates messages every interval.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="interval">Time between messages (default: 5 seconds)</param>
    /// <param name="transport">Transport proxy to route to</param>
    public TimerSource(
        ILogger<TimerSource> logger,
        TimeSpan? interval = null,
        ITransport? transport = null)
    {
        _logger = logger;
        _interval = interval ?? TimeSpan.FromSeconds(5);
        _transport = transport;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "TimerSource starting. Will generate messages every {Interval}.",
            _interval);

        _timer = new Timer(
            callback: _ => GenerateMessage(),
            state: null,
            dueTime: _interval,
            period: _interval);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "TimerSource stopping. Generated {Count} messages total.",
            _messageCount);

        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private void GenerateMessage()
    {
        var count = Interlocked.Increment(ref _messageCount);

        var message = new DataMessage
        {
            Body = $"Timer message #{count} generated at {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss.fff}",
            Metadata = new Dictionary<string, string>
            {
                ["Source"] = "TimerSource",
                ["MessageNumber"] = count.ToString(),
                ["Timestamp"] = DateTimeOffset.UtcNow.ToString("o")
            }
        };

        _logger.LogDebug("Generated message #{Count}", count);

        MessageReceived?.Invoke(message);
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
