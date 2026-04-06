using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Iris.Persistence;

/// <summary>
/// Generic SQLite-based implementation of the message store for deduplication and buffering.
/// Works with any message type that implements IPersistedMessage.
/// </summary>
/// <typeparam name="TMessage">The message type to store.</typeparam>
public sealed class SqliteMessageStore<TMessage> : IMessageStore<TMessage>, IDisposable 
    where TMessage : class, IPersistedMessage
{
    private readonly MessageStoreOptions _options;
    private readonly ILogger _logger;
    private readonly IMessageSerializationStrategy<TMessage> _serializationStrategy;
    private readonly string _connectionString;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public SqliteMessageStore(
        MessageStoreOptions options, 
        ILogger<SqliteMessageStore<TMessage>> logger,
        IMessageSerializationStrategy<TMessage> serializationStrategy)
    {
        _options = options;
        _logger = logger;
        _serializationStrategy = serializationStrategy;

        var dbDirectory = Path.GetDirectoryName(_options.DatabasePath);
        if (!string.IsNullOrEmpty(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        _connectionString = $"Data Source={_options.DatabasePath};Mode=ReadWriteCreate;";
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Enable Write-Ahead Logging for better concurrency
            using (var walCommand = connection.CreateCommand())
            {
                walCommand.CommandText = "PRAGMA journal_mode=WAL;";
                await walCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            // Create schema if it doesn't exist
            using var command = connection.CreateCommand();
            command.CommandText = _serializationStrategy.GetCreateTableSql();
            await command.ExecuteNonQueryAsync(cancellationToken);

            if (_options.Maintenance.VacuumOnStartup)
            {
                _logger.LogInformation("Running VACUUM on message store database...");
                using var vacuumCommand = connection.CreateCommand();
                vacuumCommand.CommandText = "VACUUM;";
                await vacuumCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            _logger.LogInformation("Message store initialized at {DatabasePath} for table {TableName}", 
                _options.DatabasePath, _serializationStrategy.TableName);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> HasBeenProcessedAsync(string messageId, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM {_serializationStrategy.TableName} WHERE message_id = @messageId";
        command.Parameters.AddWithValue("@messageId", messageId);

        var count = (long?)await command.ExecuteScalarAsync(cancellationToken) ?? 0;
        return count > 0;
    }

    public async Task<bool> TryStoreNewMessageAsync(TMessage message, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                // Check if message already exists (deduplication)
                using (var checkCommand = connection.CreateCommand())
                {
                    checkCommand.Transaction = transaction;
                    checkCommand.CommandText = $"SELECT COUNT(1) FROM {_serializationStrategy.TableName} WHERE message_id = @messageId";
                    checkCommand.Parameters.AddWithValue("@messageId", message.MessageId);

                    var count = (long?)await checkCommand.ExecuteScalarAsync(cancellationToken) ?? 0;
                    if (count > 0)
                    {
                        _logger.LogDebug("Duplicate message {MessageId} detected in store", message.MessageId);
                        return false;
                    }
                }

                // Insert new message
                using (var insertCommand = connection.CreateCommand())
                {
                    insertCommand.Transaction = transaction;
                    insertCommand.CommandText = _serializationStrategy.GetInsertSql();

                    // Add common parameters
                    insertCommand.Parameters.AddWithValue("@messageId", message.MessageId);
                    insertCommand.Parameters.AddWithValue("@payload", message.Payload);
                    insertCommand.Parameters.AddWithValue("@receivedAt", message.ReceivedAt.ToUnixTimeMilliseconds());
                    insertCommand.Parameters.AddWithValue("@status", (int)message.Status);
                    insertCommand.Parameters.AddWithValue("@attemptCount", message.AttemptCount);
                    insertCommand.Parameters.AddWithValue("@metadataJson", message.MetadataJson ?? (object)DBNull.Value);

                    // Add message-specific parameters
                    _serializationStrategy.AddInsertParameters(insertCommand, message);

                    await insertCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                transaction.Commit();
                _logger.LogDebug("Message {MessageId} stored successfully", message.MessageId);
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<List<TMessage>> GetPendingMessagesAsync(int limit, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT *
            FROM {_serializationStrategy.TableName}
            WHERE status IN (@pending, @failed)
            ORDER BY received_at ASC
            LIMIT @limit
        ";
        command.Parameters.AddWithValue("@pending", (int)MessageStatus.Pending);
        command.Parameters.AddWithValue("@failed", (int)MessageStatus.Failed);
        command.Parameters.AddWithValue("@limit", limit);

        var messages = new List<TMessage>();

        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            messages.Add(_serializationStrategy.MapFromReader(reader));
        }

        return messages;
    }

    public async Task MarkAsProcessingAsync(string messageId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                UPDATE {_serializationStrategy.TableName}
                SET status = @status, 
                    attempt_count = attempt_count + 1,
                    last_attempt_at = @lastAttemptAt
                WHERE message_id = @messageId
            ";
            command.Parameters.AddWithValue("@messageId", messageId);
            command.Parameters.AddWithValue("@status", (int)MessageStatus.Processing);
            command.Parameters.AddWithValue("@lastAttemptAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogDebug("Message {MessageId} marked as processing", messageId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task MarkAsCompletedAsync(string messageId, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                UPDATE {_serializationStrategy.TableName}
                SET status = @status, 
                    processed_at = @processedAt,
                    error_message = NULL
                WHERE message_id = @messageId
            ";
            command.Parameters.AddWithValue("@messageId", messageId);
            command.Parameters.AddWithValue("@status", (int)MessageStatus.Completed);
            command.Parameters.AddWithValue("@processedAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogDebug("Message {MessageId} marked as completed", messageId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task MarkAsFailedAsync(string messageId, string errorMessage, int attemptCount, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();

            // If max retries exceeded, mark as Failed permanently, otherwise keep as Pending for retry
            var status = attemptCount >= _options.Buffering.MaxRetryAttempts 
                ? MessageStatus.Failed 
                : MessageStatus.Pending;

            command.CommandText = $@"
                UPDATE {_serializationStrategy.TableName}
                SET status = @status,
                    error_message = @errorMessage,
                    attempt_count = @attemptCount,
                    last_attempt_at = @lastAttemptAt
                WHERE message_id = @messageId
            ";
            command.Parameters.AddWithValue("@messageId", messageId);
            command.Parameters.AddWithValue("@status", (int)status);
            command.Parameters.AddWithValue("@errorMessage", errorMessage);
            command.Parameters.AddWithValue("@attemptCount", attemptCount);
            command.Parameters.AddWithValue("@lastAttemptAt", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

            await command.ExecuteNonQueryAsync(cancellationToken);

            var statusText = status == MessageStatus.Failed ? "failed permanently" : "pending retry";
            _logger.LogDebug("Message {MessageId} marked as {Status} (attempt {Attempt})", 
                messageId, statusText, attemptCount);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> CleanupOldMessagesAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var cutoffTime = DateTimeOffset.UtcNow.Subtract(retention).ToUnixTimeMilliseconds();

            using var command = connection.CreateCommand();
            command.CommandText = $@"
                DELETE FROM {_serializationStrategy.TableName}
                WHERE status = @completedStatus 
                AND processed_at < @cutoffTime
            ";
            command.Parameters.AddWithValue("@completedStatus", (int)MessageStatus.Completed);
            command.Parameters.AddWithValue("@cutoffTime", cutoffTime);

            var deletedCount = await command.ExecuteNonQueryAsync(cancellationToken);

            if (deletedCount > 0)
            {
                _logger.LogInformation("Cleaned up {Count} completed messages older than {Hours}h", 
                    deletedCount, retention.TotalHours);
            }

            return deletedCount;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<MessageStoreStats> GetStatsAsync(CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var stats = new MessageStoreStats();

        using (var command = connection.CreateCommand())
        {
            command.CommandText = $@"
                SELECT 
                    COUNT(*) as total,
                    COALESCE(SUM(CASE WHEN status = @pending THEN 1 ELSE 0 END), 0) as pending,
                    COALESCE(SUM(CASE WHEN status = @completed THEN 1 ELSE 0 END), 0) as completed,
                    COALESCE(SUM(CASE WHEN status = @failed THEN 1 ELSE 0 END), 0) as failed,
                    MIN(CASE WHEN status = @pending THEN received_at END) as oldest_pending
                FROM {_serializationStrategy.TableName}
            ";
            command.Parameters.AddWithValue("@pending", (int)MessageStatus.Pending);
            command.Parameters.AddWithValue("@completed", (int)MessageStatus.Completed);
            command.Parameters.AddWithValue("@failed", (int)MessageStatus.Failed);

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                stats.TotalReceived = reader.GetInt64(0);
                stats.PendingCount = reader.GetInt64(1);
                stats.CompletedCount = reader.GetInt64(2);
                stats.FailedCount = reader.GetInt64(3);

                if (!reader.IsDBNull(4))
                {
                    var oldestPendingMs = reader.GetInt64(4);
                    stats.OldestPendingMessage = DateTimeOffset.FromUnixTimeMilliseconds(oldestPendingMs);
                }
            }
        }

        // Get database file size
        if (File.Exists(_options.DatabasePath))
        {
            stats.DatabaseSizeBytes = new FileInfo(_options.DatabasePath).Length;
        }

        return stats;
    }

    public void Dispose()
    {
        _writeLock?.Dispose();

        // Close any pooled connections
        SqliteConnection.ClearAllPools();
    }
}
