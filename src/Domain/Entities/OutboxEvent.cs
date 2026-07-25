namespace Kart.Identity.Domain.Entities;

/// <summary>
/// database-design.md `outbox_events` — Transactional Outbox (design-decisions.md,
/// "Event Publication Reliability"). Written in the same transaction as the domain
/// mutation that produced it; relayed to this service's own `identity.exchange`
/// (kart-conventions.md — one topic exchange per publishing service, no shared
/// exchange; message-bus-manifest.json) by a separate poller process (not part of
/// this vertical slice).
/// </summary>
public sealed class OutboxEvent
{
    public Guid EventId { get; private set; }
    public Guid AggregateId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "system:identity-outbox-poller";

    private OutboxEvent()
    {
    }

    public static OutboxEvent Create(Guid aggregateId, string eventType, string payloadJson, DateTimeOffset now, string createdBy) =>
        new()
        {
            EventId = Guid.NewGuid(),
            AggregateId = aggregateId,
            EventType = eventType,
            Payload = payloadJson,
            OccurredAt = now,
            CreatedBy = createdBy,
            UpdatedAt = now
        };

    /// <summary>Called by the outbox-relay poller (OutboxRelayHostedService) once a row's been published.</summary>
    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
        UpdatedAt = publishedAt;
    }
}
