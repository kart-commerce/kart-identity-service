namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a `UserRole` grant row — the persisted form of ddd-model.md's
/// `RoleGrant` value object — database-design.md `user_roles.user_role_id`.
/// </summary>
public readonly record struct UserRoleId(Guid Value) : ITypedEntityId<UserRoleId>
{
    public static UserRoleId New() => new(Guid.NewGuid());

    public static UserRoleId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
