using System.Text.Json;

namespace Kart.Identity.Infrastructure.Messaging;

/// <summary>
/// Parses contracts/message-bus-manifest.json (copied to this service's own output
/// directory as message-bus-manifest.json) into a <see cref="MessageBusManifest"/>. Fails
/// fast at startup — a missing or malformed manifest means the topology it describes
/// cannot exist, so there is no safe degraded mode to fall back to.
/// </summary>
public static class MessageBusManifestLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static MessageBusManifest Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"message-bus manifest not found at '{path}'. This service's entire RabbitMQ " +
                "topology is declared there — nothing is hardcoded in C# — so it must be present " +
                "at startup.",
                path);
        }

        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<MessageBusManifest>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"message-bus manifest at '{path}' deserialized to null.");
    }
}
