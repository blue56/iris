namespace Iris.Core.Plugins;

/// <summary>
/// Default implementation of plugin metadata.
/// </summary>
public sealed class PluginMetadata : IPluginMetadata
{
    public string Name { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0.0";
    public string Author { get; init; } = "Unknown";
    public string Description { get; init; } = string.Empty;
    public PluginType Type { get; init; }

    public static PluginMetadata FromAttribute(PluginAttribute attribute)
    {
        return new PluginMetadata
        {
            Name = attribute.Name,
            Version = attribute.Version,
            Author = attribute.Author,
            Description = attribute.Description,
            Type = attribute.Type
        };
    }
}
