using Iris.Configuration;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Iris.Core.Plugins;

/// <summary>
/// The agent loads packages and runs plugins.
/// Hosted service that discovers, loads, and registers plugins before the pipeline
/// engine starts.
/// </summary>
/// <remarks>
/// Plugins are divided into two roles:
/// <list type="bullet">
///   <item><term>Connector</term><description>Models <em>what</em> is being integrated (ASTM, LIMS, OPC-UA). Originates <see cref="DataMessage"/> items.</description></item>
///   <item><term>Transport</term><description>Models <em>how</em> data is moved (MQTT, HTTP, Kafka). Delivers messages over a protocol channel.</description></item>
/// </list>
/// All plugin assemblies are loaded through <see cref="PluginLoadContext"/> so each
/// plugin resolves its own private dependencies (MQTTnet, SQLite, etc.) from its own
/// subfolder under the configured plugin directories without requiring them to be
/// present in the host output directory.
/// </remarks>
public sealed class PluginBootstrapService : IHostedService
{
    private readonly IPluginRegistry _registry;
    private readonly UnifiedPluginFactory _factory;
    private readonly DynamicPluginLoader? _dynamicLoader;
    private readonly IServiceProvider _services;
    private readonly IOptions<IrisOptions> _options;
    private readonly ILogger<PluginBootstrapService> _logger;

    public PluginBootstrapService(
        IPluginRegistry registry,
        IPluginFactory factory,
        DynamicPluginLoader dynamicLoader,
        IServiceProvider services,
        IOptions<IrisOptions> options,
        ILogger<PluginBootstrapService> logger)
    {
        _registry = registry;
        _factory = factory as UnifiedPluginFactory ?? throw new ArgumentException("Factory must be UnifiedPluginFactory");
        _dynamicLoader = dynamicLoader;
        _services = services;
        _options = options;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bootstrapping plugin system...");

        // Load all plugins from the configured plugin directories through the isolated
        // PluginLoadContext. Each plugin DLL resolves its own private dependencies
        // from its own directory, keeping the host output directory clean.
        await LoadPluginsAsync(cancellationToken);

        // Instantiate and register plugins based on configuration
        await LoadBuiltInPluginsAsync(cancellationToken);

        var connectors = _registry.GetConnectors().Count();
        var transports = _registry.GetTransports().Count();

        _logger.LogInformation(
            "Plugin bootstrap complete. Loaded {ConnectorCount} connector(s) and {TransportCount} transport(s).",
            connectors, transports);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Plugin system shutting down.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Scans all configured plugin directories and loads every assembly through
    /// <see cref="PluginLoadContext"/> so private dependencies (MQTTnet, SQLite, …)
    /// are resolved from the plugin's own directory and never from the host context.
    /// </summary>
    private async Task LoadPluginsAsync(CancellationToken cancellationToken)
    {
        if (_dynamicLoader == null)
        {
            _logger.LogWarning("DynamicPluginLoader is not available; no plugins will be loaded.");
            return;
        }

        var config = _options.Value.PluginSystem.DynamicLoading;
        _logger.LogInformation("Scanning plugin directories: {Directories}",
            string.Join(", ", config.PackageDirectories));

        var allPluginTypes = new List<PluginTypeInfo>();

        foreach (var directory in config.PackageDirectories)
        {
            var fullPath = Path.IsPathRooted(directory)
                ? directory
                : Path.Combine(AppContext.BaseDirectory, directory);

            try
            {
                var pluginTypes = await _dynamicLoader.LoadPluginsFromDirectoryAsync(
                    fullPath,
                    config.SearchPattern);

                allPluginTypes.AddRange(pluginTypes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load plugins from directory: {Directory}", fullPath);
            }
        }

        if (allPluginTypes.Count > 0)
        {
            _factory.RegisterDynamicPlugins(allPluginTypes);
            _logger.LogInformation("Registered {Count} plugin type(s).", allPluginTypes.Count);
        }
        else
        {
            _logger.LogWarning("No plugins found in configured plugin directories.");
        }
    }

    private async Task LoadBuiltInPluginsAsync(CancellationToken cancellationToken)
    {
        await RunPluginActivatorsAsync(cancellationToken);
    }

    private async Task RunPluginActivatorsAsync(CancellationToken cancellationToken)
    {
        if (_dynamicLoader == null) return;

        var activatorInterface = typeof(IPluginActivator);
        foreach (var assembly in _dynamicLoader.GetLoadedAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

            foreach (var type in types.Where(t => t.IsClass && !t.IsAbstract && activatorInterface.IsAssignableFrom(t)))
            {
                try
                {
                    var activator = (IPluginActivator)ActivatorUtilities.CreateInstance(_services, type);
                    await activator.ActivatePluginsAsync(_factory, _registry, _services, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to run plugin activator: {Type}", type.FullName);
                }
            }
        }
    }
}
