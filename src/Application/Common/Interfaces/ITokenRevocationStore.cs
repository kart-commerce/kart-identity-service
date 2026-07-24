namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure against the shared Redis
/// deployment (design-decisions.md, "Shared State-Store Technology for Ephemeral
/// Security State", `identity:revocation:*`) — the write side of the Gateway's
/// per-request stale-token check (edge-cases.md, "Stale Revocation Under Stateless
/// JWT Validation").
/// </summary>
public interface ITokenRevocationStore
{
    /// <summary>
    /// Revokes one specific already-issued access token by its `jti` claim —
    /// api-contract.yaml POST /auth/logout only ever addresses the one token the
    /// caller presented, never every token the subject holds.
    /// </summary>
    Task RevokeTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>
    /// Marks every access token minted for <paramref name="userId"/> at or before
    /// <paramref name="revokedAt"/> as revoked, for operations with no single
    /// presented token to address by `jti` (admin-lock, password-reset-confirm) —
    /// architecture.md's Dependencies table: "Identity writes on logout, forced
    /// role-change, and admin-lock" to this same shared key space, one entry per
    /// case shape. The Gateway's read side (comparing a token's `iat` against this
    /// marker) is out of scope for this service (architecture.md, "Gateway-side
    /// revocation-list consumption is out of scope for this service's own ticket
    /// list").
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}
