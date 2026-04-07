using Iris.Configuration;
using Iris.Core.Plugins;
using Iris.Plugins.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Iris.Plugins;

/// <summary>
/// Activates built-in plugin instances based on their own configuration options.
/// Binds plugin option types directly from IConfiguration so the core never references them.
/// </summary>
public sealed class BuiltInPluginActivator : IPluginActivator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<BuiltInPluginActivator> _logger;

    public BuiltInPluginActivator(IConfiguration configuration, ILogger<BuiltInPluginActivator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task ActivatePluginsAsync(
        UnifiedPluginFactory factory,
        IPluginRegistry registry,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var fileReader = _configuration.GetSection("Sources:FileReader").Get<FileReaderOptions>() ?? new FileReaderOptions();
        if (fileReader.Enabled)
        {
            var source = factory.CreateSource("FileReader", services);
            if (source != null)
                registry.RegisterSource(source);
        }

        var fileWriter = _configuration.GetSection("Targets:FileWriter").Get<FileWriterOptions>() ?? new FileWriterOptions();
        if (!string.IsNullOrWhiteSpace(fileWriter.OutputPath))
        {
            var target = factory.CreateTarget("FileWriter", services);
            if (target != null)
                registry.RegisterTarget(target);
        }

        var mqttListener = _configuration.GetSection("Sources:MqttListener").Get<MqttListenerOptions>() ?? new MqttListenerOptions();
        if (mqttListener.Enabled)
        {
            var source = factory.CreateSource("MqttListener", services);
            if (source != null)
                registry.RegisterSource(source);
            else
                _logger.LogWarning(
                    "MqttListener is enabled but not available. " +
                    "Ensure Iris.Plugins.dll and its dependencies are present in the plugins directory.");
        }

        var mqtt = _configuration.GetSection("Targets:Mqtt").Get<MqttOptions>() ?? new MqttOptions();
        if (!string.IsNullOrWhiteSpace(mqtt.BrokerHost))
        {
            var target = factory.CreateTarget("Mqtt", services);
            if (target != null)
                registry.RegisterTarget(target);
            else
                _logger.LogWarning(
                    "Mqtt target is configured but not available. " +
                    "Ensure Iris.Plugins.dll and its dependencies are present in the plugins directory.");
        }

        await Task.CompletedTask;
    }
}
