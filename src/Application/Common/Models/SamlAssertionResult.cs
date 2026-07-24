namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// The claims extracted from a signature-verified, condition-checked SAML
/// Assertion (api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs).
/// </summary>
public sealed record SamlAssertionResult(
    string AssertionId,
    string NameId,
    IReadOnlyCollection<string> GroupClaims,
    DateTimeOffset NotOnOrAfter);
