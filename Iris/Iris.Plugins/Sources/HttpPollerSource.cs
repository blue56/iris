using Iris.Core;
using Iris.Core.Plugins;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;

namespace Iris.Plugins.Sources;

/// <summary>
/// Example plugin that polls an HTTP endpoint for data.
/// Useful for integrating with REST APIs.
/// </summary>
[Plugin("HttpPollerSource", "1.0.0", PluginType.Source,
    Author = "Iris Plugins",
    Description = "Polls an HTTP endpoint at regular intervals and forwards the response")]
public sealed class HttpPollerSource : ISource, IDisposable
{
    private readonly ILogger<HttpPollerSource> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _url;
    private readonly TimeSpan _pollInterval;
    private readonly List<string> _targetNames;
    private Timer? _timer;
    private bool _enabled;

    public event Func<DataMessage, Task>? MessageReceived;
    public IReadOnlyList<string> TargetNames => _targetNames;

    /// <summary>
    /// Creates an HTTP poller source.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <param name="url">URL to poll</param>
    /// <param name="pollInterval">Interval between polls (default: 30 seconds)</param>
    /// <param name="targetNames">Target names to route to</param>
    public HttpPollerSource(
        ILogger<HttpPollerSource> logger,
        HttpClient httpClient,
        string url,
        TimeSpan? pollInterval = null,
        List<string>? targetNames = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _url = url;
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(30);
        _targetNames = targetNames ?? [];
        _enabled = false;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_url))
        {
            _logger.LogWarning("HttpPollerSource is configured but URL is empty. Skipping.");
            return Task.CompletedTask;
        }

        _enabled = true;
        _logger.LogInformation(
            "HttpPollerSource starting. Polling {Url} every {Interval}.",
            _url, _pollInterval);

        _timer = new Timer(
            callback: async _ => await PollEndpointAsync(),
            state: null,
            dueTime: TimeSpan.Zero,
            period: _pollInterval);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _enabled = false;
        _logger.LogInformation("HttpPollerSource stopping.");
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        return Task.CompletedTask;
    }

    private async Task PollEndpointAsync()
    {
        if (!_enabled) return;

        try
        {
            _logger.LogDebug("Polling {Url}...", _url);

            var response = await _httpClient.GetAsync(_url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();

            var message = new DataMessage
            {
                Body = content,
                Metadata = new Dictionary<string, string>
                {
                    ["Source"] = "HttpPollerSource",
                    ["Url"] = _url,
                    ["StatusCode"] = ((int)response.StatusCode).ToString(),
                    ["ContentType"] = response.Content.Headers.ContentType?.ToString() ?? "unknown",
                    ["Timestamp"] = DateTimeOffset.UtcNow.ToString("o")
                }
            };

            _logger.LogInformation(
                "Received {Length} bytes from {Url} (Status: {Status})",
                content.Length, _url, response.StatusCode);

            await (MessageReceived?.Invoke(message) ?? Task.CompletedTask);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error polling {Url}", _url);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
