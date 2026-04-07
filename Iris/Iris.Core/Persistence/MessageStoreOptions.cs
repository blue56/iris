namespace Iris.Persistence;

/// <summary>
/// Configuration options for the MQTT message store.
/// </summary>
public sealed class MessageStoreOptions
{
    /// <summary>Enable the persistent message store for deduplication and buffering.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Path to the SQLite database file.</summary>
    public string DatabasePath { get; set; } = "data/mqtt_messages.db";

    /// <summary>Deduplication settings.</summary>
    public DeduplicationOptions Deduplication { get; set; } = new();

    /// <summary>Buffering and retry settings.</summary>
    public BufferingOptions Buffering { get; set; } = new();

    /// <summary>Maintenance and cleanup settings.</summary>
    public MaintenanceOptions Maintenance { get; set; } = new();
}

/// <summary>
/// Settings for message deduplication.
/// </summary>
public sealed class DeduplicationOptions
{
    /// <summary>Enable deduplication checking.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How long to keep completed messages for deduplication (in hours).</summary>
    public int RetentionHours { get; set; } = 24;
}

/// <summary>
/// Settings for message buffering and retry logic.
/// </summary>
public sealed class BufferingOptions
{
    /// <summary>Enable buffering of messages for retry on failure.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum number of delivery attempts before marking as failed.</summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>Delay in seconds before retrying a failed message.</summary>
    public int RetryDelaySeconds { get; set; } = 10;

    /// <summary>Maximum number of pending messages to keep in buffer.</summary>
    public int MaxBacklogSize { get; set; } = 10000;

    /// <summary>Replay pending messages from the buffer on application startup.</summary>
    public bool ReplayOnStartup { get; set; } = true;
}

/// <summary>
/// Settings for database maintenance and cleanup.
/// </summary>
public sealed class MaintenanceOptions
{
    /// <summary>Enable automatic cleanup of old completed messages.</summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>How often to run cleanup (in minutes).</summary>
    public int CleanupIntervalMinutes { get; set; } = 60;

    /// <summary>Run VACUUM on the database during startup to reclaim space.</summary>
    public bool VacuumOnStartup { get; set; } = false;
}

/// <summary>
/// Statistics about the message store.
/// </summary>
public sealed class MessageStoreStats
{
    /// <summary>Total number of messages received.</summary>
    public long TotalReceived { get; set; }

    /// <summary>Number of duplicate messages detected and skipped.</summary>
    public long DuplicatesDetected { get; set; }

    /// <summary>Current number of pending messages in buffer.</summary>
    public long PendingCount { get; set; }

    /// <summary>Total number of successfully completed messages.</summary>
    public long CompletedCount { get; set; }

    /// <summary>Total number of failed messages (exceeded retry limit).</summary>
    public long FailedCount { get; set; }

    /// <summary>Size of the database file in bytes.</summary>
    public long DatabaseSizeBytes { get; set; }

    /// <summary>Timestamp of the oldest pending message.</summary>
    public DateTimeOffset? OldestPendingMessage { get; set; }
}
