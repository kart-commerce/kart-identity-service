using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Exchanges an OIDC authorization code for tokens at the provider's token
/// endpoint and validates the returned id_token — Identity's only synchronous
/// outbound call to an external IdP for the OIDC flows (design-decisions.md,
/// "Resilience Pattern for External IdP Calls": per-provider circuit breaker,
/// bulkhead, and timeout budget, keyed on <see cref="OidcProviderDescriptor.ProviderKey"/>).
/// Owned by Application, implemented by Infrastructure (HTTP + JWT validation are
/// both infrastructure concerns) — same dependency-inversion shape as
/// <see cref="ISamlAssertionValidator"/>.
/// </summary>
public interface IOidcTokenExchangeClient
{
    /// <summary>
    /// Throws an Application-layer exception (never lets a raw HTTP/JWT library
    /// exception escape) on any failure: non-success token-endpoint response,
    /// missing/malformed id_token, or a signature/issuer/audience/expiry check
    /// that fails.
    /// </summary>
    Task<OidcIdentityResult> ExchangeCodeAsync(
        OidcProviderDescriptor provider, string code, DateTimeOffset now, CancellationToken cancellationToken);
}
