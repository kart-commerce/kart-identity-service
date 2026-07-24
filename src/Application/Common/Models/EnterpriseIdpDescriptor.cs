namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// One configured enterprise IdP (Okta/Azure AD/Google Workspace are the BRD's
/// named examples, not an exhaustive list) — `idpAlias` is also the
/// bulkhead/circuit-breaker key for OIDC federation (design-decisions.md,
/// "Resilience Pattern for External IdP Calls"); SAML itself makes no outbound
/// call to the IdP (see <see cref="Kart.Identity.Application.Common.Interfaces.ISamlAssertionValidator"/>),
/// so that pattern only has a call site when <see cref="Protocol"/> is
/// <see cref="EnterpriseIdpProtocol.Oidc"/> (<see cref="Oidc"/> is then non-null;
/// <see cref="SsoUrl"/>/<see cref="SpEntityId"/>/<see cref="AssertionConsumerServiceUrl"/>/
/// <see cref="SigningCertificatePem"/> apply only to <see cref="EnterpriseIdpProtocol.Saml"/>).
/// </summary>
public sealed record EnterpriseIdpDescriptor(
    string IdpAlias,
    string SsoUrl,
    string SpEntityId,
    string AssertionConsumerServiceUrl,
    string SigningCertificatePem,
    EnterpriseIdpProtocol Protocol = EnterpriseIdpProtocol.Saml,
    OidcProviderDescriptor? Oidc = null);
