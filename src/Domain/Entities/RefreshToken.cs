namespace Kart.Identity.Domain.Entities;

/// <summary>
/// The `RefreshToken` rotation-chain child entity of the `Session` aggregate
/// (ddd-model.md) — database-design.md `refresh_tokens`. Only `token_hash` is ever
/// persisted, never the raw opaque token (same never-store-the-live-secret
/// principle as `users.password_hash`).
/// </summary>
public sealed class RefreshToken
{
    public Guid TokenId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid? ParentTokenId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private RefreshToken()
    {
    }

    /// <summary>
    /// A session's first-issued token (`parent_token_id` NULL). `expires_at` is
    /// `min(issued_at + 30d, session.absolute_expires_at)` per database-design.md's
    /// native sliding-window rule.
    /// </summary>
    public static RefreshToken IssueInitial(
        Guid sessionId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset sessionAbsoluteExpiresAt,
        string createdBy)
    {
        var slidingExpiry = now.AddDays(Session.NativeSlidingWindowDays);
        var expiresAt = slidingExpiry < sessionAbsoluteExpiresAt ? slidingExpiry : sessionAbsoluteExpiresAt;

        return new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            SessionId = sessionId,
            ParentTokenId = null,
            TokenHash = tokenHash,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy
        };
    }

    /// <summary>
    /// api-contract.yaml POST /auth/refresh — the token a rotation write mints to
    /// replace <paramref name="parentTokenId"/> (edge-cases.md, "Refresh Token
    /// Replay After Rotation": the chain this creates is what family reuse
    /// detection walks). Same `min(issued_at + 30d, session.absolute_expires_at)`
    /// sliding-window formula as <see cref="IssueInitial"/> — it already handles
    /// the federated (24h absolute, no sliding) case uniformly since the session's
    /// own absolute cap is always the tighter bound there.
    /// </summary>
    public static RefreshToken IssueRotated(
        Guid sessionId,
        Guid parentTokenId,
        string tokenHash,
        DateTimeOffset now,
        DateTimeOffset sessionAbsoluteExpiresAt,
        string createdBy)
    {
        var slidingExpiry = now.AddDays(Session.NativeSlidingWindowDays);
        var expiresAt = slidingExpiry < sessionAbsoluteExpiresAt ? slidingExpiry : sessionAbsoluteExpiresAt;

        return new RefreshToken
        {
            TokenId = Guid.NewGuid(),
            SessionId = sessionId,
            ParentTokenId = parentTokenId,
            TokenHash = tokenHash,
            IssuedAt = now,
            ExpiresAt = expiresAt,
            CreatedBy = createdBy,
            UpdatedAt = now,
            UpdatedBy = createdBy
        };
    }

    /// <summary>
    /// Marks this token spent and points it at its successor. `ConsumedAt` is
    /// mapped as an EF concurrency token (RefreshTokenConfiguration) — the
    /// generated `UPDATE ... WHERE consumed_at IS NULL` this produces on
    /// <c>SaveChanges</c> IS design-decisions.md's "Concurrency Control for
    /// Refresh-Token Rotation" DB-level conditional update, not a separate
    /// mechanism layered on top of it. Callers must only call this after
    /// observing <see cref="ConsumedAt"/> as null; a concurrent winner shows up as
    /// <c>DbUpdateConcurrencyException</c> from <c>SaveChanges</c>, not from here.
    /// </summary>
    public void Consume(DateTimeOffset now, Guid replacedByTokenId, string updatedBy)
    {
        ConsumedAt = now;
        ReplacedByTokenId = replacedByTokenId;
        UpdatedAt = now;
        UpdatedBy = updatedBy;
    }
}
