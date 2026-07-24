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
}
