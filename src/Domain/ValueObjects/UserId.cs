namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for the `UserIdentity` aggregate root (ddd-model.md) —
/// database-design.md `users.user_id`. Replaces a raw <see cref="Guid"/> so a
/// <see cref="SessionId"/>, <see cref="UserRoleId"/>, etc. can never be passed where a
/// <see cref="UserId"/> is expected (or vice versa) without the compiler catching it.
/// </summary>
public readonly record struct UserId(Guid Value) : ITypedEntityId<UserId>
{
    public static UserId New() => new(Guid.NewGuid());

    public static UserId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
