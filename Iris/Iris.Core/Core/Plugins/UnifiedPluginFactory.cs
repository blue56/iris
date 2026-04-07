using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Iris.Core.Plugins;

/// <summary>
/// Unified plugin factory that supports both built-in and dynamically loaded plugins.
/// </summary>
public sealed class UnifiedPluginFactory : IPluginFactory
{
    private readonly ILogger<UnifiedPluginFactory> _logger;
    private readonly Dictionary<string, Type> _sourceTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _targetTypes = new(StringComparer.OrdinalIgnoreCase);

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
        int sourceCount = 0;
        int targetCount = 0;

        foreach (var pluginInfo in pluginTypes)
        {
            var friendlyName = pluginInfo.Metadata.Name;

            if (pluginInfo.IsSource)
            {
                RegisterSourceType(friendlyName, pluginInfo.Type);
                sourceCount++;
            }

            if (pluginInfo.IsTarget)
            {
                RegisterTargetType(friendlyName, pluginInfo.Type);
                targetCount++;
            }
        }

        _logger.LogInformation(
            "Registered {SourceCount} dynamic source(s) and {TargetCount} dynamic target(s)",
            sourceCount, targetCount);
    }

    /// <summary>
    /// Registers a source plugin type.
    /// </summary>
    public void RegisterSourceType(string name, Type type)
    {
        if (_sourceTypes.ContainsKey(name))
        {
            _logger.LogWarning("Source type '{Name}' is already registered. Overwriting with {Type}", name, type.Name);
        }

        _sourceTypes[name] = type;
        _logger.LogDebug("Registered source type: {Name} -> {Type}", name, type.FullName);
    }

    /// <summary>
    /// Registers a target plugin type.
    /// </summary>
    public void RegisterTargetType(string name, Type type)
    {
        if (_targetTypes.ContainsKey(name))
        {
            _logger.LogWarning("Target type '{Name}' is already registered. Overwriting with {Type}", name, type.Name);
        }

        _targetTypes[name] = type;
        _logger.LogDebug("Registered target type: {Name} -> {Type}", name, type.FullName);
    }

    public ISource? CreateSource(string typeName, IServiceProvider services)
    {
        if (!_sourceTypes.TryGetValue(typeName, out var type))
        {
            _logger.LogWarning("Unknown source type: {TypeName}. Available types: {Types}",
                typeName, string.Join(", ", _sourceTypes.Keys));
            return null;
        }

        try
        {
            var instance = ActivatorUtilities.CreateInstance(services, type);
            _logger.LogDebug("Created source instance: {TypeName} ({Type})", typeName, type.Name);
            return instance as ISource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create source of type {TypeName}", typeName);
            return null;
        }
    }

    public ITarget? CreateTarget(string typeName, IServiceProvider services)
    {
        if (!_targetTypes.TryGetValue(typeName, out var type))
        {
            _logger.LogWarning("Unknown target type: {TypeName}. Available types: {Types}",
                typeName, string.Join(", ", _targetTypes.Keys));
            return null;
        }

        try
        {
            var instance = ActivatorUtilities.CreateInstance(services, type);
            _logger.LogDebug("Created target instance: {TypeName} ({Type})", typeName, type.Name);
            return instance as ITarget;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create target of type {TypeName}", typeName);
            return null;
        }
    }

    public IEnumerable<string> GetAvailableSourceTypes() => _sourceTypes.Keys;

    public IEnumerable<string> GetAvailableTargetTypes() => _targetTypes.Keys;
}
