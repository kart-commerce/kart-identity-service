namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for an `OutboxEvent` row — database-design.md
/// `outbox_events.event_id`. Note this is distinct from `OutboxEvent.AggregateId`, which
/// deliberately stays a raw <see cref="Guid"/>: it points at whichever aggregate root (a
/// <see cref="UserId"/>, <see cref="SessionId"/>, ...) emitted the event, so it is inherently
/// polymorphic and cannot itself be given one specific strong type.
/// </summary>
public readonly record struct OutboxEventId(Guid Value) : ITypedEntityId<OutboxEventId>
{
    public static OutboxEventId New() => new(Guid.NewGuid());

    public static OutboxEventId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
