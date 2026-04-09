using Iris.Core.Plugins;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Iris.Core;

/// <summary>
/// The agent loads packages and runs plugins.
/// Wires each registered <see cref="IConnector"/> (domain integration — what the
/// agent talks to) to its configured <see cref="ITransport"/> instances (protocol
/// channels — how data is moved) and runs as a hosted background service.
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

        var connectors = _registry.GetConnectors().ToList();

        foreach (var connector in connectors)
        {
            var transport = connector.Transport;
            if (transport != null)
            {
                connector.MessageReceived += msg => DispatchToTransportAsync(msg, transport, stoppingToken);
                transport.MessageReceived += msg => DispatchToConnectorAsync(msg, connector, stoppingToken);

                await transport.StartAsync(stoppingToken);
            }

            await connector.StartAsync(stoppingToken);
        }

        _logger.LogInformation("All connectors and coupled transports active. Waiting for data...");

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        _logger.LogInformation("Iris pipeline engine stopping.");

        foreach (var connector in connectors)
        {
            await connector.StopAsync(CancellationToken.None);
            if (connector.Transport != null)
            {
                await connector.Transport.StopAsync(CancellationToken.None);
            }
        }
    }

    private async Task DispatchToTransportAsync(DataMessage message, ITransport transport, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispatching message {Id} to transport {TransportName}.", message.Id, transport.Name);
        await transport.SendAsync(message, cancellationToken);
    }

    private async Task DispatchToConnectorAsync(DataMessage message, IConnector connector, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Dispatching message {Id} from transport to connector {Connector}.", message.Id, connector.GetType().Name);
        await connector.SendAsync(message, cancellationToken);
    }
}
