namespace Kart.Identity.Domain.Entities;

/// <summary>
/// database-design.md `password_reset_tokens` — modeled in PostgreSQL (not Redis)
/// for the same strong-consistency single-use semantics as `refresh_tokens`, via
/// the same DB-conditional-update pattern (`ConsumedAt` as an EF concurrency
/// token, see PasswordResetTokenConfiguration).
/// </summary>
public sealed class PasswordResetToken
{
    /// <summary>
    /// Engineering default: neither requirement-spec.md nor edge-cases.md name a
    /// concrete reset-token validity window.
    /// </summary>
    public const int ValidityMinutes = 60;

    public Guid ResetTokenId { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? ConsumedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private PasswordResetToken()
    {
    }

    /// <summary>
    /// api-contract.yaml POST /auth/password/reset-initiate. `createdBy`/`updatedBy`
    /// are always the owning user_id (database-design.md: "a reset token is always
    /// requested by... that same user").
    /// </summary>
    public static PasswordResetToken Issue(Guid userId, string tokenHash, DateTimeOffset now)
    {
        var owner = userId.ToString();
        return new PasswordResetToken
        {
            ResetTokenId = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            IssuedAt = now,
            ExpiresAt = now.AddMinutes(ValidityMinutes),
            CreatedBy = owner,
            UpdatedAt = now,
            UpdatedBy = owner
        };
    }

    /// <summary>
    /// Marks this token spent. `ConsumedAt` is mapped as an EF concurrency token
    /// (PasswordResetTokenConfiguration) — callers must only call this after
    /// observing <see cref="ConsumedAt"/> as null; a concurrent winner shows up as
    /// <c>DbUpdateConcurrencyException</c> from <c>SaveChanges</c>, not from here
    /// (same shape as <see cref="RefreshToken.Consume"/>).
    /// </summary>
    public void Consume(DateTimeOffset now)
    {
        ConsumedAt = now;
        UpdatedAt = now;
        UpdatedBy = UserId.ToString();
    }
}
