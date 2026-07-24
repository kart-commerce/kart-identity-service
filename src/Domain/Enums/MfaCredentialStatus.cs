namespace Kart.Identity.Domain.Enums;

/// <summary>
/// database-design.md `mfa_credentials.status` — 'pending' rows exist between
/// POST /auth/mfa/enroll and .../enroll/confirm; an unconfirmed pending row
/// expires and must be restarted, it never silently activates.
/// </summary>
public enum MfaCredentialStatus
{
    Pending,
    Active
}
