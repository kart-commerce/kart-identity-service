using Kart.Identity.Domain.Enums;

namespace Kart.Identity.Domain.Entities;

/// <summary>
/// database-design.md `user_roles` — the persisted `RoleGrant` value objects of the
/// `UserIdentity` aggregate (ddd-model.md). At most one live (non-revoked) row per
/// (user, role), enforced by `uq_user_roles_live`.
/// </summary>
public sealed class UserRole
{
    public Guid UserRoleId { get; private set; }
    public Guid UserId { get; private set; }
    public PlatformRole Role { get; private set; }
    public DateTimeOffset GrantedAt { get; private set; }
    public string GrantedBy { get; private set; } = string.Empty;
    public DateTimeOffset? RevokedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = string.Empty;

    private UserRole()
    {
    }

    /// <summary>ddd-model.md's `RoleGrant` value object — `(Role, GrantedAt, GrantedBy)`.</summary>
    public static UserRole Grant(Guid userId, PlatformRole role, string grantedBy, DateTimeOffset now) =>
        new()
        {
            UserRoleId = Guid.NewGuid(),
            UserId = userId,
            Role = role,
            GrantedAt = now,
            GrantedBy = grantedBy,
            UpdatedAt = now,
            UpdatedBy = grantedBy
        };

    /// <summary>
    /// database-design.md: native self-registration always grants exactly
    /// `Customer`, `granted_by = 'self-registration'`.
    /// </summary>
    public static UserRole GrantSelfRegisteredCustomer(Guid userId, DateTimeOffset now) =>
        Grant(userId, PlatformRole.Customer, grantedBy: "self-registration", now);
}
