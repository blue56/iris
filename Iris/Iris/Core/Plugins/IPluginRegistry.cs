namespace Iris.Core.Plugins;

/// <summary>
/// Central registry for managing loaded plugins.
/// </summary>
public interface IPluginRegistry
{
    /// <summary>Register a source plugin.</summary>
    void RegisterSource(ISource source);

    /// <summary>Register a target plugin.</summary>
    void RegisterTarget(ITarget target);

    /// <summary>Get all registered sources.</summary>
    IEnumerable<ISource> GetSources();

    /// <summary>Get all registered targets.</summary>
    IEnumerable<ITarget> GetTargets();

    /// <summary>Get a specific target by name.</summary>
    ITarget? GetTarget(string name);

    /// <summary>Get metadata for all registered plugins.</summary>
    IEnumerable<IPluginMetadata> GetAllPluginMetadata();
}
