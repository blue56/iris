using Iris.Core.Plugins;
using Iris.Plugins.Connectors;
using Iris.Plugins.Sources;
using Iris.Plugins.Targets;

namespace Iris.Plugins;

/// <summary>
/// Registers all built-in plugin types from Iris.Plugins with the host factory.
/// Called by PluginBootstrapService at startup via IPluginRegistrar discovery.
/// </summary>
/// <remarks>
/// Connectors model domain integrations (<em>what</em> the agent talks to).
/// Transports model protocol delivery channels (<em>how</em> data moves).
/// </remarks>
public sealed class BuiltInPluginRegistrar : IPluginRegistrar
{
    public void RegisterPlugins(UnifiedPluginFactory factory)
    {
        // Connectors — domain integrations (what the agent talks to)
        factory.RegisterConnectorType("FilesystemWatcher", typeof(FilesystemWatcherConnector));
        factory.RegisterConnectorType("HttpPoller",    typeof(HttpPollerSource));
        factory.RegisterConnectorType("Timer",         typeof(TimerSource));
        factory.RegisterConnectorType("FileWriter",    typeof(FileWriterConnector));

        // Transports — protocol channels (how data moves)
        factory.RegisterTransportType("Mqtt",          typeof(MqttTransport));
        factory.RegisterTransportType("HttpWebhook",   typeof(HttpWebhookTarget));
        factory.RegisterTransportType("Console",       typeof(ConsoleTarget));
    }
}
