namespace Iris.Persistence;

/// <summary>
/// Generic persistent store for messages that provides deduplication and buffering capabilities.
/// Can be implemented for any message type (MQTT, HTTP, custom protocols, etc.).
/// </summary>
/// <typeparam name="TMessage">The type of message to store. Must implement IPersistedMessage.</typeparam>
public interface IMessageStore<TMessage> where TMessage : class, IPersistedMessage
{
    /// <summary>
    /// Initialize the message store (create tables, indexes, etc.).
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Check if a message with the given ID has already been processed.
    /// </summary>
    /// <param name="messageId">The unique message identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message has been processed before, false otherwise.</returns>
    Task<bool> HasBeenProcessedAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Attempt to store a new message. Returns false if the message already exists (duplicate).
    /// This operation should be atomic - it checks for duplicates and stores in a single transaction.
    /// </summary>
    /// <param name="message">The message to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was stored (new), false if it was a duplicate.</returns>
    Task<bool> TryStoreNewMessageAsync(TMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Get pending messages that need to be processed or retried.
    /// </summary>
    /// <param name="limit">Maximum number of messages to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending messages ordered by received time.</returns>
    Task<List<TMessage>> GetPendingMessagesAsync(int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Mark a message as currently being processed.
    /// </summary>
    Task MarkAsProcessingAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Mark a message as successfully completed.
    /// </summary>
    Task MarkAsCompletedAsync(string messageId, CancellationToken cancellationToken);

    /// <summary>
    /// Mark a message as failed with error details and increment attempt count.
    /// </summary>
    Task MarkAsFailedAsync(string messageId, string errorMessage, int attemptCount, CancellationToken cancellationToken);

    /// <summary>
    /// Delete completed messages older than the specified retention period.
    /// </summary>
    /// <param name="retention">How long to keep completed messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of messages deleted.</returns>
    Task<int> CleanupOldMessagesAsync(TimeSpan retention, CancellationToken cancellationToken);

    /// <summary>
    /// Get statistics about the message store.
    /// </summary>
    Task<MessageStoreStats> GetStatsAsync(CancellationToken cancellationToken);
}
