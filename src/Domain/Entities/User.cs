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
}
