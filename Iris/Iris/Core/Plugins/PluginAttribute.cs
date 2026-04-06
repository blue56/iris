namespace Iris.Core.Plugins;

/// <summary>
/// Attribute to mark and describe plugin classes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PluginAttribute : Attribute
{
    public string Name { get; }
    public string Version { get; }
    public string Author { get; set; } = "Unknown";
    public string Description { get; set; } = string.Empty;
    public PluginType Type { get; }

    public PluginAttribute(string name, string version, PluginType type)
    {
        Name = name;
        Version = version;
        Type = type;
    }
}
