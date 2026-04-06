namespace Iris.Core.Plugins;

/// <summary>
/// Defines the type of plugin functionality.
/// </summary>
public enum PluginType
{
    /// <summary>Plugin acts as a data source.</summary>
    Source,

    /// <summary>Plugin acts as a data target.</summary>
    Target,

    /// <summary>Plugin can act as both source and target.</summary>
    Both
}
