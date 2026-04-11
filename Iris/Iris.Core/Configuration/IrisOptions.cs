namespace Iris.Configuration;

public sealed class IrisOptions
{
    public LoggingOptions Logging { get; set; } = new();
    public PluginSystemOptions PluginSystem { get; set; } = new();
}

public sealed class PluginSystemOptions
{
    public DynamicLoadingOptions DynamicLoading { get; set; } = new();
}

public sealed class DynamicLoadingOptions
{
    /// <summary>Enable dynamic plugin loading from external assemblies.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Directories to scan for plugin DLL files.</summary>
    public List<string> PackageDirectories { get; set; } = [];

    /// <summary>File pattern for plugin DLLs (default: *.dll).</summary>
    public string SearchPattern { get; set; } = "*.dll";

    /// <summary>Whether to scan subdirectories for plugins.</summary>
    public bool ScanSubdirectories { get; set; } = false;
}

public sealed class LoggingOptions
{
    public string FilePath { get; set; } = "logs/iris-.log";
    public string RollingInterval { get; set; } = "Day";
}
