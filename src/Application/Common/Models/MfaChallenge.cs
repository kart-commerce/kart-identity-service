namespace Kart.Identity.Application.Common.Models;

/// <summary>api-contract.yaml `components.schemas.MfaChallenge` — server-side-only state key (edge-cases.md, "Partial-Auth Window During MFA"); no token is issued for this intermediate state.</summary>
public sealed record MfaChallenge(string ChallengeId, int ExpiresInSeconds);
