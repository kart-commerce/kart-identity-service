namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for an `IdpGroupRoleMapping` — database-design.md
/// `idp_group_role_mappings.mapping_id`.
/// </summary>
public readonly record struct IdpGroupRoleMappingId(Guid Value) : ITypedEntityId<IdpGroupRoleMappingId>
{
    public static IdpGroupRoleMappingId New() => new(Guid.NewGuid());

    public static IdpGroupRoleMappingId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
