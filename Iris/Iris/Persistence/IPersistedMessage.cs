namespace Iris.Persistence;

/// <summary>
/// Base interface for messages that can be persisted in the message store.
/// Implement this interface for any message type that needs deduplication and buffering.
/// </summary>
public interface IPersistedMessage
{
    /// <summary>Unique identifier for this message (from payload or generated).</summary>
    string MessageId { get; set; }

    /// <summary>Raw message payload/body.</summary>
    string Payload { get; set; }

    /// <summary>When the message was first received.</summary>
    DateTimeOffset ReceivedAt { get; set; }

    /// <summary>Current processing status.</summary>
    MessageStatus Status { get; set; }

    /// <summary>Number of delivery attempts made.</summary>
    int AttemptCount { get; set; }

    /// <summary>Timestamp of the last delivery attempt.</summary>
    DateTimeOffset? LastAttemptAt { get; set; }

    /// <summary>Error message from the last failed attempt.</summary>
    string? ErrorMessage { get; set; }

    /// <summary>When the message was successfully processed and delivered.</summary>
    DateTimeOffset? ProcessedAt { get; set; }

    /// <summary>Additional metadata as JSON string.</summary>
    string? MetadataJson { get; set; }
}

/// <summary>
/// Processing status for messages in the store.
/// </summary>
public enum MessageStatus
{
    /// <summary>Message received but not yet processed.</summary>
    Pending = 0,

    /// <summary>Message is currently being processed.</summary>
    Processing = 1,

    /// <summary>Message successfully delivered to all sinks.</summary>
    Completed = 2,

    /// <summary>Message failed after all retry attempts.</summary>
    Failed = 3
}
