using Iris.Configuration;
using Iris.Core;
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
        var connectorsSection = _configuration.GetSection("Connectors");
        foreach (var child in connectorsSection.GetChildren())
        {
            var type = child["type"] ?? child.Key;

            ITransport? transport = null;
            var transportSection = child.GetSection("Transport");
            if (transportSection.Exists())
            {
                var tType = transportSection["type"] ?? "Unknown";
                if (string.Equals(tType, "Mqtt", StringComparison.OrdinalIgnoreCase))
                {
                    var options = transportSection.Get<MqttOptions>() ?? new MqttOptions();
                    if (!string.IsNullOrWhiteSpace(options.BrokerHost))
                    {
                        transport = factory.CreateTransport("Mqtt", services, options);
                    }
                }
            }

            if (string.Equals(type, "FilesystemWatcher", StringComparison.OrdinalIgnoreCase))
            {
                var options = child.Get<FilesystemWatcherOptions>() ?? new FilesystemWatcherOptions();
                options.Name = child.Key;
                if (options.Enabled)
                {
                    var connector = factory.CreateConnector("FilesystemWatcher", services, options, transport!);
                    if (connector != null) registry.RegisterConnector(connector);
                }
            }
            else if (string.Equals(type, "FileWriter", StringComparison.OrdinalIgnoreCase))
            {
                var options = child.Get<FileWriterOptions>() ?? new FileWriterOptions();
                options.Name = child.Key;
                if (!string.IsNullOrWhiteSpace(options.OutputPath))
                {
                    var connector = factory.CreateConnector("FileWriter", services, options, transport!);
                    if (connector != null) registry.RegisterConnector(connector);
                }
            }
        }

        await Task.CompletedTask;
    }
}
