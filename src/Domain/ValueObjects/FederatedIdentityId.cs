namespace Kart.Identity.Domain.ValueObjects;

/// <summary>
/// Strongly-typed identifier for a `FederatedIdentity` — database-design.md
/// `federated_identities.federated_identity_id`.
/// </summary>
public readonly record struct FederatedIdentityId(Guid Value) : ITypedEntityId<FederatedIdentityId>
{
    public static FederatedIdentityId New() => new(Guid.NewGuid());

    public static FederatedIdentityId From(Guid value) => new(value);

    public override string ToString() => Value.ToString();
}
