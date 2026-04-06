using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;

namespace Iris.Core.Plugins;

/// <summary>
/// An AssemblyLoadContext that resolves dependency DLLs from the same
/// directory as the plugin being loaded, falling back to the default context
/// for any assembly that lives outside the plugin directory (i.e. host / shared
/// framework assemblies such as Iris.dll, Microsoft.Extensions.*, etc.).
/// This prevents duplicate type identities for types shared between host and plugin.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly string _pluginDirectory;

    public PluginLoadContext(string pluginPath) : base(isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
        _pluginDirectory = Path.GetDirectoryName(pluginPath)!;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

        // Only load from this context if the DLL lives inside the plugin directory.
        // Assemblies resolved to the host output directory (Iris.dll, Microsoft.Extensions.*,
        // etc.) must come from the default context so their types are identical to the ones
        // already registered in the DI container.
        if (assemblyPath != null &&
            assemblyPath.StartsWith(_pluginDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Returning null delegates to the default (host) context.
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null &&
            libraryPath.StartsWith(_pluginDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}

/// <summary>
/// Loads plugins dynamically from external assemblies at runtime.
/// Uses AssemblyLoadContext for proper assembly isolation.
/// </summary>
public sealed class DynamicPluginLoader : IDisposable
{
    private readonly ILogger<DynamicPluginLoader> _logger;
    private readonly List<AssemblyLoadContext> _loadContexts = [];
    private readonly List<Assembly> _loadedAssemblies = [];

    public DynamicPluginLoader(ILogger<DynamicPluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Scans a directory and loads all plugin assemblies.
    /// </summary>
    /// <param name="directory">Directory containing plugin DLL files</param>
    /// <param name="searchPattern">File pattern to search for (default: *.dll)</param>
    /// <returns>List of discovered plugin types with their metadata</returns>
    public async Task<List<PluginTypeInfo>> LoadPluginsFromDirectoryAsync(
        string directory, 
        string searchPattern = "*.dll")
    {
        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Plugin directory does not exist: {Directory}", directory);
            return [];
        }

        _logger.LogInformation("Scanning for plugins in: {Directory}", directory);

        var pluginTypes = new List<PluginTypeInfo>();
        var dllFiles = Directory.GetFiles(directory, searchPattern);

        foreach (var dllPath in dllFiles)
        {
            try
            {
                var types = await LoadPluginsFromAssemblyAsync(dllPath);
                pluginTypes.AddRange(types);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugins from assembly: {Assembly}", dllPath);
            }
        }

        _logger.LogInformation(
            "Plugin discovery complete. Found {Count} plugin(s) from {AssemblyCount} assembly(ies)",
            pluginTypes.Count, _loadedAssemblies.Count);

        return pluginTypes;
    }

    /// <summary>
    /// Loads plugins from a specific assembly file.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly DLL</param>
    /// <returns>List of discovered plugin types with their metadata</returns>
    public async Task<List<PluginTypeInfo>> LoadPluginsFromAssemblyAsync(string assemblyPath)
    {
        _logger.LogDebug("Loading assembly: {Assembly}", assemblyPath);

        Assembly assembly;

        try
        {
            // Use a PluginLoadContext so dependency DLLs are resolved from the
            // plugin's own directory before falling back to the host context.
            var loadContext = new PluginLoadContext(assemblyPath);
            _loadContexts.Add(loadContext);

            assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            _loadedAssemblies.Add(assembly);

            _logger.LogInformation("Loaded assembly: {AssemblyName} v{Version}",
                assembly.GetName().Name, assembly.GetName().Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load assembly: {Assembly}", assemblyPath);
            throw;
        }

        // Scan assembly for plugin types
        var pluginTypes = await Task.Run(() => ScanAssemblyForPlugins(assembly));

        _logger.LogInformation(
            "Discovered {Count} plugin(s) in assembly: {AssemblyName}",
            pluginTypes.Count, assembly.GetName().Name);

        return pluginTypes;
    }

    /// <summary>
    /// Scans an assembly for types with the [Plugin] attribute.
    /// </summary>
    private List<PluginTypeInfo> ScanAssemblyForPlugins(Assembly assembly)
    {
        var pluginTypes = new List<PluginTypeInfo>();

        // Use the successfully-loaded subset when a partial load failure occurs
        // so that non-plugin types with broken dependencies don't block plugin discovery.
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogWarning(
                "Assembly '{Assembly}' could not fully load. Scanning {Count} successfully loaded type(s). " +
                "Failures: {Errors}",
                assembly.GetName().Name,
                ex.Types.Count(t => t != null),
                string.Join("; ", ex.LoaderExceptions
                    .Where(e => e != null)
                    .Select(e => e!.Message)
                    .Distinct()));

            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (var type in types)
        {
            // Look for [Plugin] attribute
            var pluginAttribute = type.GetCustomAttribute<PluginAttribute>();
            if (pluginAttribute == null)
                continue;

            // Validate that type implements ISource or ITarget
            bool isSource = typeof(ISource).IsAssignableFrom(type);
            bool isTarget = typeof(ITarget).IsAssignableFrom(type);

            if (!isSource && !isTarget)
            {
                _logger.LogWarning(
                    "Type {Type} has [Plugin] attribute but doesn't implement ISource or ITarget. Skipping.",
                    type.FullName);
                continue;
            }

            // Validate plugin type matches implementation
            var expectedType = isSource && isTarget ? PluginType.Both :
                             isSource ? PluginType.Source :
                             PluginType.Target;

            if (pluginAttribute.Type != expectedType && pluginAttribute.Type != PluginType.Both)
            {
                _logger.LogWarning(
                    "Plugin {Name} declares type {Declared} but implements {Actual}",
                    pluginAttribute.Name, pluginAttribute.Type, expectedType);
            }

            var metadata = PluginMetadata.FromAttribute(pluginAttribute);
            var pluginInfo = new PluginTypeInfo
            {
                Type = type,
                Metadata = metadata,
                IsSource = isSource,
                IsTarget = isTarget,
                Assembly = assembly
            };

            pluginTypes.Add(pluginInfo);

            _logger.LogDebug(
                "Discovered plugin: {Name} v{Version} ({Type}) - {PluginType}",
                metadata.Name, metadata.Version, type.Name, metadata.Type);
        }

        return pluginTypes;
    }

    /// <summary>
    /// Gets all currently loaded plugin assemblies.
    /// </summary>
    public IReadOnlyList<Assembly> GetLoadedAssemblies() => _loadedAssemblies.AsReadOnly();

    public void Dispose()
    {
        // Note: In Phase 2, we don't unload assemblies
        // Phase 3 could implement proper unloading with custom AssemblyLoadContext
        _loadContexts.Clear();
        _loadedAssemblies.Clear();
    }
}

/// <summary>
/// Information about a discovered plugin type.
/// </summary>
public sealed class PluginTypeInfo
{
    public required Type Type { get; init; }
    public required IPluginMetadata Metadata { get; init; }
    public required bool IsSource { get; init; }
    public required bool IsTarget { get; init; }
    public required Assembly Assembly { get; init; }
}
