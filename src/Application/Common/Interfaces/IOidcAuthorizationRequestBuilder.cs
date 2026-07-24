using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Builds the SP-initiated OIDC authorization-code redirect URL — the OIDC-flavored
/// equivalent of <see cref="ISamlAuthnRequestBuilder"/>, shared by enterprise OIDC
/// federation (IDN-16's login redirect) and customer social login (IDN-17).
/// </summary>
public interface IOidcAuthorizationRequestBuilder
{
    /// <summary>
    /// <paramref name="state"/> is round-tripped by the IdP and echoed back at the
    /// callback endpoint verbatim — same "no server-side outstanding-request store"
    /// engineering default as the SAML AuthnRequest builder (edge-cases.md, "SAML
    /// Assertion Replay at the ACS Endpoint" explicitly chose a consumed-assertion-ID
    /// cache over a bound-request-state store for the equivalent SAML concern).
    /// </summary>
    string BuildRedirectUrl(OidcProviderDescriptor provider, string state);
}
