namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Verifies a submitted TOTP (RFC 6238) code against a base32 secret — the
/// counterpart to <see cref="ITotpProvisioningService"/>'s generation side.
/// Used by POST /auth/mfa/enroll/confirm (IDN-5) and, later, POST
/// /auth/mfa/verify (IDN-6).
/// </summary>
public interface ITotpCodeValidator
{
    bool IsCodeValid(string base32Secret, string code);
}
