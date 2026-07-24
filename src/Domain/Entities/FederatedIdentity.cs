using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Domain.Entities;

/// <summary>
/// database-design.md `federated_identities` — one external IdP link (enterprise
/// or social) to a `UserIdentity` (ddd-model.md), identified by
/// `(IdpType, IdpKey, ExternalSubjectId)`.
/// </summary>
public sealed class FederatedIdentity
{
    public Guid FederatedIdentityId { get; private set; }
    public Guid UserId { get; private set; }
    public FederatedIdpType IdpType { get; private set; }
    public string IdpKey { get; private set; } = string.Empty;
    public string ExternalSubjectId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private FederatedIdentity()
    {
    }

    /// <summary>
    /// JIT-created on first successful federation for an external identity with no
    /// existing link (edge-cases.md, "Federated Login With No Matching Kart
    /// Account") — always self-service, per database-design.md's own note.
    /// </summary>
    public static FederatedIdentity Link(Guid userId, FederatedIdpType idpType, string idpKey, string externalSubjectId, DateTimeOffset now)
    {
        var owner = userId.ToString();
        return new FederatedIdentity
        {
            FederatedIdentityId = Guid.NewGuid(),
            UserId = userId,
            IdpType = idpType,
            IdpKey = idpKey,
            ExternalSubjectId = externalSubjectId,
            CreatedAt = now,
            CreatedBy = owner,
            UpdatedAt = now,
            UpdatedBy = owner
        };
    }
}
