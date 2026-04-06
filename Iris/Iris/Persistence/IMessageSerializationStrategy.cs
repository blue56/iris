using Microsoft.Data.Sqlite;

namespace Iris.Persistence;

/// <summary>
/// Strategy interface for serializing/deserializing message-specific data to/from database.
/// Implement this to support custom message types with the generic SqliteMessageStore.
/// </summary>
/// <typeparam name="TMessage">The message type to serialize.</typeparam>
public interface IMessageSerializationStrategy<TMessage> where TMessage : class, IPersistedMessage
{
    /// <summary>
    /// Get the table name for this message type.
    /// </summary>
    string TableName { get; }

    /// <summary>
    /// Get the CREATE TABLE SQL statement including any message-specific columns.
    /// Must include all columns from IPersistedMessage plus any custom columns.
    /// </summary>
    string GetCreateTableSql();

    /// <summary>
    /// Get the INSERT SQL statement including message-specific columns.
    /// </summary>
    string GetInsertSql();

    /// <summary>
    /// Add parameters to the insert command for message-specific columns.
    /// Base columns (message_id, payload, received_at, status, attempt_count, etc.) are already added.
    /// </summary>
    void AddInsertParameters(SqliteCommand command, TMessage message);

    /// <summary>
    /// Map a database reader row to a message instance.
    /// Expected column order: message_id, payload, received_at, status, attempt_count, 
    /// last_attempt_at, error_message, metadata_json, processed_at, [custom columns...]
    /// </summary>
    TMessage MapFromReader(SqliteDataReader reader);
}
