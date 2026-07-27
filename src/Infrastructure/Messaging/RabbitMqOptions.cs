namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Binds the "RabbitMq" configuration section. Every exchange/queue/binding/dead-letter/
/// retry-tier name lives in contracts/message-bus-manifest.json (loaded via
/// <see cref="MessageBusManifestLoader"/>), not here — this only holds the connection
/// details the manifest itself doesn't describe.
/// </summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Path to this service's message-bus manifest. Relative paths resolve against
    /// AppContext.BaseDirectory (the manifest is copied there at build time from
    /// contracts/message-bus-manifest.json).
    /// </summary>
    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
