using Microsoft.Extensions.Logging;

namespace Iris.Core.Plugins;

/// <summary>
/// Default implementation of <see cref="IPluginRegistry"/>.
/// </summary>
/// <remarks>
/// Maintains separate collections for connectors (domain integrations — what the
/// agent talks to, e.g. ASTM, LIMS, OPC-UA) and transports (protocol channels —
/// how data is moved, e.g. MQTT, HTTP webhook, Kafka).
/// </remarks>
public sealed class PluginRegistry : IPluginRegistry
{
    private readonly List<IConnector> _connectors = [];
    private readonly List<ITransport> _transports = [];
    private readonly Dictionary<string, ITransport> _transportsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<PluginRegistry> _logger;

    public PluginRegistry(ILogger<PluginRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterConnector(IConnector connector)
    {
        _connectors.Add(connector);

        var metadata = GetMetadata(connector);
        if (metadata != null)
        {
            _logger.LogInformation(
                "Registered connector plugin: {Name} v{Version} by {Author} (as '{ConnectorName}')",
                metadata.Name, metadata.Version, metadata.Author, connector.Name);
        }
        else
        {
            _logger.LogInformation("Registered connector: {Type} (as '{ConnectorName}')", connector.GetType().Name, connector.Name);
        }
    }

    public void RegisterTransport(ITransport transport)
    {
        _transports.Add(transport);
        _transportsByName[transport.Name] = transport;

        var metadata = GetMetadata(transport);
        if (metadata != null)
        {
            _logger.LogInformation(
                "Registered transport plugin: {Name} v{Version} by {Author} (as '{TransportName}')",
                metadata.Name, metadata.Version, metadata.Author, transport.Name);
        }
        else
        {
            _logger.LogInformation("Registered transport: {Type} (as '{TransportName}')",
                transport.GetType().Name, transport.Name);
        }
    }

    public IEnumerable<IConnector> GetConnectors() => _connectors;

    public IEnumerable<ITransport> GetTransports() => _transports;

    public ITransport? GetTransport(string name)
    {
        _transportsByName.TryGetValue(name, out var transport);
        return transport;
    }

    public IEnumerable<IPluginMetadata> GetAllPluginMetadata()
    {
        var metadata = new List<IPluginMetadata>();

        foreach (var connector in _connectors)
        {
            var meta = GetMetadata(connector);
            if (meta != null)
                metadata.Add(meta);
        }

        foreach (var transport in _transports)
        {
            var meta = GetMetadata(transport);
            if (meta != null)
                metadata.Add(meta);
        }

        return metadata;
    }

    private static IPluginMetadata? GetMetadata(object obj)
    {
        var attribute = obj.GetType().GetCustomAttributes(typeof(PluginAttribute), false)
            .FirstOrDefault() as PluginAttribute;

        return attribute != null ? PluginMetadata.FromAttribute(attribute) : null;
    }
}
