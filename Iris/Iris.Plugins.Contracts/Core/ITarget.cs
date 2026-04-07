namespace Iris.Core;

/// <summary>
/// A target accepts a <see cref="DataMessage"/> and forwards it to a destination.
/// </summary>
public interface ITarget
{
    /// <summary>Unique name used to reference this target from source routing configuration.</summary>
    string Name { get; }

    /// <summary>Send the message to the destination.</summary>
    Task SendAsync(DataMessage message, CancellationToken cancellationToken);
}
