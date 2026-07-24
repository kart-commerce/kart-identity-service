namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// One configured enterprise IdP (Okta/Azure AD/Google Workspace are the BRD's
/// named examples, not an exhaustive list) — `idpAlias` is also the
/// bulkhead/circuit-breaker key for future OIDC federation (design-decisions.md,
/// "Resilience Pattern for External IdP Calls"); SAML itself makes no outbound
/// call to the IdP (see <see cref="Kart.Identity.Application.Common.Interfaces.ISamlAssertionValidator"/>),
/// so that pattern has no call site in this ticket pair.
/// </summary>
public sealed record EnterpriseIdpDescriptor(
    string IdpAlias,
    string SsoUrl,
    string SpEntityId,
    string AssertionConsumerServiceUrl,
    string SigningCertificatePem);
