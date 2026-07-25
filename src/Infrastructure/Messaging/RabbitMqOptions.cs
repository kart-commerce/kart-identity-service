namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Binds the "RabbitMq" configuration section — message-bus-manifest.json's topology names.
/// </summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; set; } = "localhost";

    /// <summary>message-bus-manifest.json: `identity.exchange`, topic, durable — this service's own published events.</summary>
    public string Exchange { get; set; } = "identity.exchange";

    /// <summary>message-bus-manifest.json: `identity.dlx`, topic, durable — dead-letters for this service's own consumer queue.</summary>
    public string DeadLetterExchange { get; set; } = "identity.dlx";

    /// <summary>
    /// message-bus-manifest.json: Identity does not own `user.exchange` (User Service does) — it only
    /// binds its own `identity.user-events.queue` to it. Declared idempotently by the consumer so the
    /// bind never fails if this service starts before User Service has created it.
    /// </summary>
    public string UserExchange { get; set; } = "user.exchange";
}
