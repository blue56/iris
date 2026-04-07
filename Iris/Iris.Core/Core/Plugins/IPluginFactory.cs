namespace Iris.Core.Plugins;

/// <summary>
/// Factory for creating plugin instances.
/// </summary>
public interface IPluginFactory
{
    /// <summary>Create a source plugin by type name.</summary>
    ISource? CreateSource(string typeName, IServiceProvider services);

    /// <summary>Create a target plugin by type name.</summary>
    ITarget? CreateTarget(string typeName, IServiceProvider services);

    /// <summary>Get all available source type names.</summary>
    IEnumerable<string> GetAvailableSourceTypes();

    /// <summary>Get all available target type names.</summary>
    IEnumerable<string> GetAvailableTargetTypes();
}
