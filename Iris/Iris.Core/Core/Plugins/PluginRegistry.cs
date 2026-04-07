using Microsoft.Extensions.Logging;

namespace Iris.Core.Plugins;

/// <summary>
/// Default implementation of the plugin registry.
/// </summary>
public sealed class PluginRegistry : IPluginRegistry
{
    private readonly List<ISource> _sources = [];
    private readonly List<ITarget> _targets = [];
    private readonly Dictionary<string, ITarget> _targetsByName = new();
    private readonly ILogger<PluginRegistry> _logger;

    public PluginRegistry(ILogger<PluginRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterSource(ISource source)
    {
        _sources.Add(source);

        var metadata = GetMetadata(source);
        if (metadata != null)
        {
            _logger.LogInformation(
                "Registered source plugin: {Name} v{Version} by {Author}",
                metadata.Name, metadata.Version, metadata.Author);
        }
        else
        {
            _logger.LogInformation("Registered source: {Type}", source.GetType().Name);
        }
    }

    public void RegisterTarget(ITarget target)
    {
        _targets.Add(target);
        _targetsByName[target.Name] = target;

        var metadata = GetMetadata(target);
        if (metadata != null)
        {
            _logger.LogInformation(
                "Registered target plugin: {Name} v{Version} by {Author} (as '{TargetName}')",
                metadata.Name, metadata.Version, metadata.Author, target.Name);
        }
        else
        {
            _logger.LogInformation("Registered target: {Type} (as '{TargetName}')",
                target.GetType().Name, target.Name);
        }
    }

    public IEnumerable<ISource> GetSources() => _sources;

    public IEnumerable<ITarget> GetTargets() => _targets;

    public ITarget? GetTarget(string name)
    {
        _targetsByName.TryGetValue(name, out var target);
        return target;
    }

    public IEnumerable<IPluginMetadata> GetAllPluginMetadata()
    {
        var metadata = new List<IPluginMetadata>();

        foreach (var source in _sources)
        {
            var meta = GetMetadata(source);
            if (meta != null)
                metadata.Add(meta);
        }

        foreach (var target in _targets)
        {
            var meta = GetMetadata(target);
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
