using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Server-side-only MFA partial-auth challenge state (edge-cases.md, "Partial-Auth
/// Window During MFA") — deliberately never a token; design-decisions.md's shared
/// ephemeral-security-state Redis deployment (`identity:mfa-challenge:*`).
/// </summary>
public interface IMfaChallengeStore
{
    Task<MfaChallenge> CreateAsync(Guid userId, IReadOnlyCollection<string> roles, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically reads and deletes the challenge — a challenge is single-use by
    /// construction (edge-cases.md, "Partial-Auth Window During MFA": no token
    /// exists for this state, so replaying a spent challengeId must fail exactly
    /// like an unknown one). Returns null if the challengeId is unknown, expired,
    /// or already consumed.
    /// </summary>
    Task<MfaChallengeState?> GetAndConsumeAsync(string challengeId, CancellationToken cancellationToken);
}
