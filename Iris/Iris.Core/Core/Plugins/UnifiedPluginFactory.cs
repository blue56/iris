using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Iris.Core.Plugins;

/// <summary>
/// Unified plugin factory that supports both built-in and dynamically loaded plugins.
/// </summary>
/// <remarks>
/// Maintains two separate type registries:
/// <list type="bullet">
///   <item><term>Connector types</term><description>Domain integrations that originate messages — <em>what</em> you are talking to (e.g. ASTM, LIMS, OPC-UA).</description></item>
///   <item><term>Transport types</term><description>Protocol channels that deliver messages — <em>how</em> data moves (e.g. MQTT, HTTP, Kafka).</description></item>
/// </list>
/// </remarks>
public sealed class UnifiedPluginFactory : IPluginFactory
{
    private readonly ILogger<UnifiedPluginFactory> _logger;
    private readonly Dictionary<string, Type> _transportTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _connectorTypes = new(StringComparer.OrdinalIgnoreCase);

    public UnifiedPluginFactory(ILogger<UnifiedPluginFactory> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers dynamically discovered plugins from external assemblies.
    /// </summary>
    /// <param name="pluginTypes">Plugin types discovered by DynamicPluginLoader</param>
    public void RegisterDynamicPlugins(IEnumerable<PluginTypeInfo> pluginTypes)
    {
        int transportCount = 0;
        int connectorCount = 0;

        foreach (var pluginInfo in pluginTypes)
        {
            var friendlyName = pluginInfo.Metadata.Name;

            if (pluginInfo.IsConnector)
            {
                RegisterConnectorType(friendlyName, pluginInfo.Type);
                connectorCount++;
            }

            if (pluginInfo.IsTransport)
            {
                RegisterTransportType(friendlyName, pluginInfo.Type);
                transportCount++;
            }
        }

        _logger.LogInformation(
            "Registered {ConnectorCount} dynamic connector(s) and {TransportCount} dynamic transport(s)",
            connectorCount, transportCount);
    }

    /// <summary>
    /// Registers a transport plugin type.
    /// </summary>
    public void RegisterTransportType(string name, Type type)
    {
        if (_transportTypes.ContainsKey(name))
        {
            _logger.LogWarning("Transport type '{Name}' is already registered. Overwriting with {Type}", name, type.Name);
        }

        _transportTypes[name] = type;
        _logger.LogDebug("Registered transport type: {Name} -> {Type}", name, type.FullName);
    }

    /// <summary>
    /// Registers a connector plugin type.
    /// </summary>
    public void RegisterConnectorType(string name, Type type)
    {
        if (_connectorTypes.ContainsKey(name))
        {
            _logger.LogWarning("Connector type '{Name}' is already registered. Overwriting with {Type}", name, type.Name);
        }

        _connectorTypes[name] = type;
        _logger.LogDebug("Registered connector type: {Name} -> {Type}", name, type.FullName);
    }

    public ITransport? CreateTransport(string typeName, IServiceProvider services, params object[] parameters)
    {
        if (!_transportTypes.TryGetValue(typeName, out var type))
        {
            _logger.LogWarning("Unknown transport type: {TypeName}. Available types: {Types}",
                typeName, string.Join(", ", _transportTypes.Keys));
            return null;
        }

        try
        {
            var instance = ActivatorUtilities.CreateInstance(services, type, parameters);
            _logger.LogDebug("Created transport instance: {TypeName} ({Type})", typeName, type.Name);
            return instance as ITransport;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create transport instance: {TypeName} ({Type})", typeName, type.Name);
            return null;
        }
    }

    public IConnector? CreateConnector(string typeName, IServiceProvider services, params object[] parameters)
    {
        if (!_connectorTypes.TryGetValue(typeName, out var type))
        {
            _logger.LogWarning("Unknown connector type: {TypeName}. Available types: {Types}",
                typeName, string.Join(", ", _connectorTypes.Keys));
            return null;
        }

        try
        {
            var instance = ActivatorUtilities.CreateInstance(services, type, parameters);
            _logger.LogDebug("Created connector instance: {TypeName} ({Type})", typeName, type.Name);
            return instance as IConnector;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create connector instance: {TypeName} ({Type})", typeName, type.Name);
            return null;
        }
    }

    public IEnumerable<string> GetAvailableTransportTypes() => _transportTypes.Keys;

    public IEnumerable<string> GetAvailableConnectorTypes() => _connectorTypes.Keys;
}
