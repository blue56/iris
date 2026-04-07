namespace Iris.Core;

/// <summary>
/// A source produces <see cref="DataMessage"/> items and emits them via the
/// <see cref="MessageReceived"/> event.
/// </summary>
public interface ISource
{
    /// <summary>Raised whenever a new message is available.</summary>
    event Func<DataMessage, Task> MessageReceived;

    /// <summary>
    /// Names of the targets this source routes messages to.
    /// An empty list means route to all registered targets.
    /// </summary>
    IReadOnlyList<string> TargetNames { get; }

    /// <summary>Start listening / watching for data.</summary>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>Stop the source gracefully.</summary>
    Task StopAsync(CancellationToken cancellationToken);
}
