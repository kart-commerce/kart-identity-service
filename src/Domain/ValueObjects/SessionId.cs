namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for the `Session` aggregate root (ddd-model.md) —
/// database-design.md `sessions.session_id`.
/// </summary>
public readonly record struct SessionId(Guid Value) : ITypedEntityId<SessionId>
{
    public static SessionId New() => new(Guid.NewGuid());

    public static SessionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
