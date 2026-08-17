using System.Diagnostics;
using Kart.Identity.Domain.ValueObjects;

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
    public OutboxEventId EventId { get; private set; }

    /// <summary>
    /// The aggregate root that raised this event — a <see cref="UserId"/>,
    /// <see cref="SessionId"/>, etc. depending on <see cref="EventType"/>. Deliberately a raw
    /// <see cref="Guid"/> rather than one of this domain's strongly-typed IDs: a single outbox
    /// table carries events for every aggregate type in this service, so there is no one type
    /// to strengthen it to without erasing that polymorphism.
    /// </summary>
    public Guid AggregateId { get; private set; }

    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>
    /// The originating request/consumer's W3C traceparent, captured here at the single
    /// Create choke point (not at every handler call site) — the outbox relay runs on its own
    /// background-poller async context, seconds later, where `Activity.Current` is meaningless.
    /// Read back at relay time via `Kart.Shared.Messaging.RabbitMqTraceContext.
    /// StartPublishActivityFromStoredTraceParent` so the eventual RabbitMQ consumer's span
    /// continues the same trace the original HTTP request started.
    /// </summary>
    public string? TraceParent { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "system:identity-outbox-poller";

    private OutboxEvent()
    {
    }

    public static OutboxEvent Create(Guid aggregateId, string eventType, string payloadJson, DateTimeOffset now, string createdBy) =>
        new()
        {
            EventId = OutboxEventId.New(),
            AggregateId = aggregateId,
            EventType = eventType,
            Payload = payloadJson,
            OccurredAt = now,
            TraceParent = Activity.Current?.Id,
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
