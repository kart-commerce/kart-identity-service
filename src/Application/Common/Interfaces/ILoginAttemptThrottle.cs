namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Per-account and per-IP progressive throttling on /auth/login
/// (edge-cases.md, "Credential Stuffing / Brute-Force on /auth/login"),
/// design-decisions.md's shared ephemeral-security-state Redis deployment.
/// </summary>
public interface ILoginAttemptThrottle
{
    Task<bool> IsBlockedAsync(string email, string ipAddress, CancellationToken cancellationToken);

    Task RecordFailureAsync(string email, string ipAddress, CancellationToken cancellationToken);

    Task ResetAsync(string email, string ipAddress, CancellationToken cancellationToken);
}
