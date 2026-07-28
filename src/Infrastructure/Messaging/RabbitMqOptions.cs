namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Binds the "RabbitMq" configuration section. Every exchange/queue/binding/dead-letter/
/// retry-tier name lives in contracts/message-bus-manifest.json (loaded via
/// <see cref="MessageBusManifestLoader"/>), not here — this only holds the connection
/// details the manifest itself doesn't describe. <see cref="UserName"/>/<see cref="Password"/>
/// are supplied via the GlobalConfig file (never committed, same tier as ConnectionStrings/
/// Jwt:SigningKey/Mfa:Encryption) — <see cref="HostName"/> and <see cref="ManifestPath"/> are
/// non-secret and may live in appsettings.json.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// Dedicated non-guest broker credentials. RabbitMQ's default "guest" user is
    /// restricted to loopback-only connections, so any broker reached over a real
    /// network hop (container bridge, k8s service DNS, etc.) needs a real user —
    /// same reasoning the sibling services already apply. Left unset, RabbitMQ.Client
    /// falls back to its own guest/guest default, which only works for a broker on
    /// literal 127.0.0.1.
    /// </summary>
    public string? UserName { get; set; }

    public string? Password { get; set; }

    /// <summary>
    /// Path to this service's message-bus manifest. Relative paths resolve against
    /// AppContext.BaseDirectory (the manifest is copied there at build time from
    /// contracts/message-bus-manifest.json).
    /// </summary>
    public string ManifestPath { get; set; } = "message-bus-manifest.json";
}
