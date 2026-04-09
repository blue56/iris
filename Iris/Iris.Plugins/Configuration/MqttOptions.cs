using Iris.Persistence;

using Iris.Persistence;

namespace Iris.Plugins.Configuration;

public sealed class MqttOptions
{
    public string Name { get; set; } = "mqtt";
    public string BrokerHost { get; set; } = string.Empty;
    public int BrokerPort { get; set; } = 1883;
    public string Topic { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Password { get; set; }

    // Listening options
    public bool Enabled { get; set; } = false;
    public string SubscribeTopic { get; set; } = string.Empty;
    public MessageStoreOptions? MessageStore { get; set; }
}
