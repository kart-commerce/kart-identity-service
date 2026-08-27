namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Per-account and per-IP progressive throttling on /auth/otp/request and
/// /auth/otp/verify alike (same brute-force/spam concern ILoginAttemptThrottle
/// addresses for /auth/login, deliberately not shared — this codebase's own
/// duplicate-per-slice precedent, see ILoginAttemptThrottle). Unlike login (which
/// only records a failed password), every OTP request or verify attempt counts —
/// requesting codes is itself the resource being protected (email/SMS spam), and a
/// verify attempt against a 6-digit code needs its own brute-force ceiling.
/// </summary>
public interface IOtpAttemptThrottle
{
    Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken);

    Task RecordAttemptAsync(string email, string ipAddress, CancellationToken cancellationToken);

    Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken);
}
