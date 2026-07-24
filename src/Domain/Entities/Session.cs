using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Domain.Entities;

/// <summary>
/// `Session` aggregate root (ddd-model.md) — database-design.md `sessions`. One row
/// per successful authentication; session id is always freshly generated, never
/// carried over from a pre-auth session (edge-cases.md, "Session Fixation via
/// Pre-Auth Session Reuse").
/// </summary>
public sealed class Session
{
    /// <summary>requirement-spec.md §4: native refresh-token absolute cap.</summary>
    public const int NativeAbsoluteCapDays = 90;

    /// <summary>requirement-spec.md §4: native refresh-token sliding window.</summary>
    public const int NativeSlidingWindowDays = 30;

    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsFederated { get; private set; }
    public string? IdpAlias { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset AbsoluteExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public SessionRevocationReason? RevokedReason { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private Session()
    {
    }

    /// <summary>
    /// api-contract.yaml POST /auth/register mints a session immediately (Customer
    /// role, MFA off by default) — this is always a native, non-federated session.
    /// </summary>
    public static Session CreateNative(Guid userId, DateTimeOffset now)
    {
        var owner = userId.ToString();
        return new Session
        {
            SessionId = Guid.NewGuid(),
            UserId = userId,
            IsFederated = false,
            CreatedAt = now,
            AbsoluteExpiresAt = now.AddDays(NativeAbsoluteCapDays),
            CreatedBy = owner,
            UpdatedAt = now,
            UpdatedBy = owner
        };
    }

    /// <summary>
    /// edge-cases.md, "Refresh Token Replay After Rotation": reuse of an
    /// already-rotated-out token revokes "that token's entire family" — since
    /// every refresh token in a rotation chain shares this one `session_id`
    /// (never reassigned across rotations), revoking the session itself IS
    /// revoking the whole family; no per-token fan-out write is needed.
    /// </summary>
    public void Revoke(SessionRevocationReason reason, DateTimeOffset now, string revokedBy)
    {
        RevokedAt = now;
        RevokedReason = reason;
        UpdatedAt = now;
        UpdatedBy = revokedBy;
    }
}
