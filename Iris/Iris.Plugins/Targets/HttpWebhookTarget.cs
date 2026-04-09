using Iris.Core;
using Iris.Core.Plugins;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Iris.Plugins.Targets;

/// <summary>
/// Example plugin that posts messages to an HTTP endpoint.
/// Useful for webhook integrations.
/// </summary>
[Plugin("HttpWebhookTarget", "1.0.0", PluginType.Transport,
    Author = "Iris Plugins",
    Description = "Posts messages to an HTTP webhook endpoint")]
public sealed class HttpWebhookTarget : ITransport
{
    private readonly ILogger<HttpWebhookTarget> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _webhookUrl;
    private readonly string _name;
    private readonly int _maxRetries;

    public event Func<DataMessage, Task>? MessageReceived;

    public string Name => _name;

    /// <summary>
    /// Creates an HTTP webhook target.
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="httpClient">HTTP client for making requests</param>
    /// <param name="webhookUrl">Webhook URL to post to</param>
    /// <param name="name">Target name (default: "webhook")</param>
    /// <param name="maxRetries">Maximum retry attempts (default: 3)</param>
    public HttpWebhookTarget(
        ILogger<HttpWebhookTarget> logger,
        HttpClient httpClient,
        string webhookUrl,
        string? name = null,
        int maxRetries = 3)
    {
        _logger = logger;
        _httpClient = httpClient;
        _webhookUrl = webhookUrl;
        _name = name ?? "webhook";
        _maxRetries = maxRetries;
    }

    public async Task SendAsync(DataMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_webhookUrl))
        {
            _logger.LogWarning("Cannot send message {Id} - webhook URL is not configured.", message.Id);
            return;
        }

        var payload = new
        {
            messageId = message.Id,
            body = message.Body,
            metadata = message.Metadata,
            timestamp = DateTimeOffset.UtcNow
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        for (int attempt = 1; attempt <= _maxRetries; attempt++)
        {
            try
            {
                _logger.LogDebug(
                    "Posting message {Id} to {Url} (attempt {Attempt}/{Max})",
                    message.Id, _webhookUrl, attempt, _maxRetries);

                var response = await _httpClient.PostAsync(_webhookUrl, content, cancellationToken);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation(
                    "Successfully posted message {Id} to {Url} (Status: {Status})",
                    message.Id, _webhookUrl, response.StatusCode);

                return; // Success!
            }
            catch (Exception ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to post message {Id} to {Url} (attempt {Attempt}/{Max}). Retrying...",
                    message.Id, _webhookUrl, attempt, _maxRetries);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to post message {Id} to {Url} after {Attempts} attempts.",
                    message.Id, _webhookUrl, _maxRetries);
                throw;
            }
        }
    }
}
