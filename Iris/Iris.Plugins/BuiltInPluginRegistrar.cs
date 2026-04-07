using Iris.Core.Plugins;
using Iris.Plugins.Sources;
using Iris.Plugins.Targets;

namespace Iris.Plugins;

/// <summary>
/// Registers all built-in plugin types from Iris.Plugins with the host factory.
/// Called by PluginBootstrapService at startup via IPluginRegistrar discovery.
/// </summary>
public sealed class BuiltInPluginRegistrar : IPluginRegistrar
{
    public void RegisterPlugins(UnifiedPluginFactory factory)
    {
        factory.RegisterSourceType("FileReader",    typeof(FileReaderSource));
        factory.RegisterSourceType("MqttListener",  typeof(MqttListenerSource));
        factory.RegisterSourceType("HttpPoller",    typeof(HttpPollerSource));
        factory.RegisterSourceType("Timer",         typeof(TimerSource));

        factory.RegisterTargetType("FileWriter",    typeof(FileWriterTarget));
        factory.RegisterTargetType("Mqtt",          typeof(MqttTarget));
        factory.RegisterTargetType("HttpWebhook",   typeof(HttpWebhookTarget));
        factory.RegisterTargetType("Console",       typeof(ConsoleTarget));
    }
}
