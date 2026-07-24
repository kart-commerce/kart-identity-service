namespace Kart.Identity.Domain.Enums;

/// <summary>
/// database-design.md `sessions.revoked_reason` — 'erasure' added by ddd-model.md
/// as a proposed, additive vocabulary extension for the (not-yet-built)
/// `UserDataErased` consumer.
/// </summary>
public enum SessionRevocationReason
{
    Logout,
    ReuseDetected,
    AdminLock,
    RoleChange,
    PasswordReset,
    Erasure
}
