namespace Kart.Identity.Application.Features.Login;

/// <summary>api-contract.yaml POST /auth/login 202 — `components.schemas.MfaChallenge`. No token issued (edge-cases.md, "Partial-Auth Window During MFA").</summary>
public sealed record MfaChallengeLoginResult(string ChallengeId, int ExpiresInSeconds) : LoginResult;
