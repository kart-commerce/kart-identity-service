namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// One configured OIDC relying-party registration — shared shape for an enterprise
/// IdP configured for OIDC (IDN-16) and a customer social-login provider (IDN-17/18):
/// both are "exchange an authorization code at a token endpoint, validate the
/// returned id_token" flows, differing only in what the caller does with the
/// resulting identity afterward (role-mapping lookup vs. fixed Customer grant).
/// <paramref name="ProviderKey"/> doubles as the per-provider resilience
/// (circuit breaker/bulkhead/timeout) key, mirroring <see cref="EnterpriseIdpDescriptor"/>'s
/// use of idpAlias for the same purpose (design-decisions.md, "Resilience Pattern
/// for External IdP Calls").
/// </summary>
public sealed record OidcProviderDescriptor(
    string ProviderKey,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string ClientId,
    string ClientSecret,
    string RedirectUri,
    string Issuer,
    string SigningCertificatePem);
