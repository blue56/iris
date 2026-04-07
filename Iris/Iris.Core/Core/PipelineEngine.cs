using Iris.Core.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Iris.Core;

/// <summary>
/// Wires each registered <see cref="ISource"/> to its configured subset of
/// <see cref="ITarget"/> instances and runs as a hosted background service.
/// </summary>
public sealed class PipelineEngine : BackgroundService
{
    private readonly IPluginRegistry _registry;
    private readonly ILogger<PipelineEngine> _logger;

    public PipelineEngine(
        IPluginRegistry registry,
        ILogger<PipelineEngine> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Iris pipeline engine starting.");

        var sources = _registry.GetSources();

        foreach (var source in sources)
        {
            var resolvedTargets = ResolveTargets(source);
            source.MessageReceived += msg => DispatchAsync(msg, resolvedTargets, stoppingToken);
            await source.StartAsync(stoppingToken);
        }

        _logger.LogInformation("All sources active. Waiting for data...");

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        _logger.LogInformation("Iris pipeline engine stopping.");

        foreach (var source in sources)
            await source.StopAsync(CancellationToken.None);
    }

    /// <summary>
    /// Returns the targets configured for the given source.
    /// If the source declares no target names, all registered targets are used as a fallback.
    /// </summary>
    private IReadOnlyList<ITarget> ResolveTargets(ISource source)
    {
        var names = source.TargetNames;

        if (names.Count == 0)
        {
            _logger.LogWarning(
                "Source {Source} has no targets configured. Routing to all registered targets.",
                source.GetType().Name);
            return _registry.GetTargets().ToList();
        }

        var resolved = new List<ITarget>();
        foreach (var name in names)
        {
            var target = _registry.GetTarget(name);
            if (target != null)
                resolved.Add(target);
            else
                _logger.LogWarning("Source {Source} references unknown target '{Target}'.", source.GetType().Name, name);
        }

        return resolved;
    }

    private async Task DispatchAsync(DataMessage message, IReadOnlyList<ITarget> targets, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispatching message {Id} to {TargetCount} target(s).", message.Id, targets.Count);

        var tasks = targets.Select(target => target.SendAsync(message, cancellationToken));
        await Task.WhenAll(tasks);
    }
}
