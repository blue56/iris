using System;
using System.Threading;
using System.Threading.Tasks;
using Iris.Core;
using Iris.Core.Plugins;
using Iris.Plugins.Configuration;
using Iris.Plugins.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Iris.Plugins.Targets;

/// <summary>
/// Forwards <see cref="DataMessage"/> payloads to an MQTT topic.
/// </summary>
[Plugin("Mqtt", "1.0.0", PluginType.Target,
    Author = "Iris Team",
    Description = "Publishes messages to an MQTT broker")]
public sealed class MqttTarget : ITarget, IDisposable
{
    private readonly MqttOptions _options;
    private readonly ILogger<MqttTarget> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private MqttMessageQueueClient? _mqttClient;
    private bool _connected;

    public string Name => _options.Name;

    public MqttTarget(IConfiguration configuration, ILogger<MqttTarget> logger, ILoggerFactory loggerFactory)
    {
        _options = configuration.GetSection("Targets:Mqtt").Get<MqttOptions>() ?? new MqttOptions();
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task SendAsync(DataMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BrokerHost))
        {
            _logger.LogWarning("Cannot send message {Id} - MQTT BrokerHost is not configured.", message.Id);
            return;
        }

        if (!_connected)
        {
            _mqttClient = new MqttMessageQueueClient(
                _options.BrokerHost,
                _options.BrokerPort,
                _options.Topic,
                _options.Username,
                _options.Password,
                _loggerFactory.CreateLogger<MqttMessageQueueClient>());

            await _mqttClient.ConnectAsync(cancellationToken);
            _connected = true;
        }

        await _mqttClient.PublishAsync(message, cancellationToken);
        _logger.LogInformation("Message {Id} sent to MQTT topic {Topic}.", message.Id, _options.Topic);
    }

    public void Dispose()
    {
        _mqttClient?.Dispose();
    }
}
