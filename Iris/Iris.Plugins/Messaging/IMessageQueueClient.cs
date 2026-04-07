using System.Threading;
using System.Threading.Tasks;
using Iris.Core;

namespace Iris.Plugins.Messaging;

/// <summary>
/// Abstraction for a message queue client (SQS, MQTT, etc.).
/// </summary>
public interface IMessageQueueClient
{
    Task ConnectAsync(CancellationToken cancellationToken);
    Task PublishAsync(DataMessage message, CancellationToken cancellationToken);
    Task SubscribeAsync(Func<DataMessage, Task> onMessage, CancellationToken cancellationToken);
}
