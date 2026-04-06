namespace Iris.Persistence;

/// <summary>
/// Extracts message IDs from messages for deduplication.
/// Implement this interface to provide custom message ID extraction logic for different message types.
/// </summary>
/// <typeparam name="TMessage">The type of message to extract IDs from.</typeparam>
public interface IMessageIdExtractor<in TMessage> where TMessage : class
{
    /// <summary>
    /// Extract or generate a unique message ID from the given message.
    /// </summary>
    /// <param name="message">The message to extract the ID from.</param>
    /// <returns>A unique message identifier.</returns>
    string ExtractMessageId(TMessage message);
}
