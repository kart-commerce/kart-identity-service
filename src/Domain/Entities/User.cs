using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Domain.Entities;

/// <summary>
/// `UserIdentity` aggregate root (ddd-model.md) — database-design.md `users`.
/// </summary>
public sealed class User
{
    public Guid UserId { get; private set; }
    public string? Email { get; private set; }
    public string? PasswordHash { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public AccountOrigin AccountOrigin { get; private set; }
    public DateTimeOffset? LockedAt { get; private set; }
    public string? LockedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    private User()
    {
    }

    /// <summary>
    /// api-contract.yaml POST /auth/register. database-design.md: a self-registered
    /// row has no prior authenticated caller to attribute the insert to, so
    /// created_by/updated_by are stamped with the row's own newly-generated user_id.
    /// </summary>
    public static User RegisterNative(string email, string passwordHash, string displayName, DateTimeOffset now)
    {
        var userId = Guid.NewGuid();
        var self = userId.ToString();

        return new User
        {
            UserId = userId,
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            AccountOrigin = AccountOrigin.Native,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = self,
            UpdatedBy = self
        };
    }

    /// <summary>
    /// api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs — JIT
    /// provisioning on first successful federation for an external identity with
    /// no existing Kart account (edge-cases.md, "Federated Login With No Matching
    /// Kart Account"). No password (federated accounts never gain a native one,
    /// database-design.md); email is nullable since an enterprise assertion is
    /// not guaranteed to carry an email claim.
    /// </summary>
    public static User ProvisionFederated(string? email, string displayName, AccountOrigin accountOrigin, DateTimeOffset now)
    {
        var userId = Guid.NewGuid();
        var self = userId.ToString();

        return new User
        {
            UserId = userId,
            Email = email,
            PasswordHash = null,
            DisplayName = displayName,
            AccountOrigin = accountOrigin,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = self,
            UpdatedBy = self
        };
    }

    /// <summary>
    /// api-contract.yaml POST /internal/users/{userId}/lock — ADR-0010,
    /// requirement-spec.md §2's admin-suspension FR. <paramref name="lockedBy"/> is
    /// Admin Service's own service-principal client_id, from the calling
    /// client-credentials token.
    /// </summary>
    public void Lock(DateTimeOffset now, string lockedBy)
    {
        LockedAt = now;
        LockedBy = lockedBy;
        UpdatedAt = now;
        UpdatedBy = lockedBy;
    }

    /// <summary>api-contract.yaml POST /internal/users/{userId}/unlock.</summary>
    public void Unlock(DateTimeOffset now, string unlockedBy)
    {
        LockedAt = null;
        LockedBy = null;
        UpdatedAt = now;
        UpdatedBy = unlockedBy;
    }

    /// <summary>
    /// api-contract.yaml POST /auth/password/reset-confirm. Always the owning
    /// user themself (database-design.md: a reset is always performed by/on
    /// behalf of that same user).
    /// </summary>
    public void SetPassword(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        UpdatedAt = now;
        UpdatedBy = UserId.ToString();
    }

    /// <summary>
    /// api-contract.yaml PATCH /auth/profile — always the owning user themself
    /// (bearer auth as that user is the only gate, requirement-spec.md §2/§4,
    /// ADR-0006). Only the fields the caller supplied change; `null` means "leave
    /// as-is," not "clear the value" (api-contract.yaml's `minProperties: 1` body).
    /// </summary>
    public void UpdateProfile(string? email, string? displayName, DateTimeOffset now)
    {
        if (email is not null)
        {
            Email = email;
        }

        if (displayName is not null)
        {
            DisplayName = displayName;
        }

        UpdatedAt = now;
        UpdatedBy = UserId.ToString();
    }

    /// <summary>
    /// Consumes `UserDataErased` (ADR-0016; ddd-model.md's `UserIdentity` aggregate
    /// invariant) — tombstones the PII fields this aggregate owns. Idempotent by
    /// construction (re-applying to an already-erased row is a no-op overwrite of
    /// the same tombstone values), matching the at-least-once/idempotent-consumer
    /// reliability bar every consumed/published event on this service is held to.
    /// </summary>
    public void Erase(DateTimeOffset now)
    {
        Email = null;
        DisplayName = "[erased]";
        PasswordHash = null;
        UpdatedAt = now;
        UpdatedBy = "system:user-service-erasure-consumer";
    }
}
