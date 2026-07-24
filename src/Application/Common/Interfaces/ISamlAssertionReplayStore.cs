namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure against the shared Redis
/// deployment (design-decisions.md, "Shared State-Store Technology for
/// Ephemeral Security State", `identity:saml-assertion:*`) — the consumed-
/// assertion-ID cache edge-cases.md's "SAML Assertion Replay at the ACS
/// Endpoint" chooses, TTL'd to the assertion's own validity window.
/// </summary>
public interface ISamlAssertionReplayStore
{
    /// <summary>
    /// Atomically checks-and-marks <paramref name="assertionId"/> as consumed.
    /// Returns <c>true</c> if this call is the first to consume it (proceed),
    /// <c>false</c> if it was already consumed (replay — api-contract.yaml 409).
    /// </summary>
    Task<bool> TryConsumeAsync(string assertionId, TimeSpan ttl, CancellationToken cancellationToken);
}
