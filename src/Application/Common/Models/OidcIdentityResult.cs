namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// The verified identity extracted from a successful OIDC authorization-code
/// exchange (id_token claims, once signature/issuer/audience/expiry are all
/// validated) — <paramref name="GroupClaims"/> is consulted for enterprise
/// federation's IdP-group-to-Kart-role mapping (IDN-16, same fail-closed rule as
/// SAML's <see cref="SamlAssertionResult"/>) and always empty/ignored for social
/// login (IDN-18, which never consults external claims for role elevation).
/// </summary>
public sealed record OidcIdentityResult(
    string Subject,
    string? Email,
    IReadOnlyCollection<string> GroupClaims);
