namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// The state a Login-created <see cref="MfaChallenge"/> resolves to when consumed
/// by POST /auth/mfa/verify (IDN-6) — the `userId`/`roles` Login already resolved
/// before the MFA step, so VerifyMfa doesn't need to re-derive them.
/// </summary>
public sealed record MfaChallengeState(Guid UserId, IReadOnlyCollection<string> Roles);
