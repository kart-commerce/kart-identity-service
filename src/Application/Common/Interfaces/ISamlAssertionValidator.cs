using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure — parses and validates a
/// base64-encoded SAML 2.0 Response (signature, status, validity window,
/// audience), per api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs.
/// SAML's signed-assertion model needs no back-channel call to the IdP to do
/// this (unlike OIDC's authorization-code exchange), so this is pure local
/// cryptographic/XML validation.
/// </summary>
public interface ISamlAssertionValidator
{
    /// <summary>
    /// Throws <see cref="Kart.Identity.Application.Common.Exceptions.InvalidSamlAssertionException"/>
    /// on any signature, status, condition, or audience failure.
    /// </summary>
    SamlAssertionResult ValidateAndExtract(string samlResponseBase64, EnterpriseIdpDescriptor idp, DateTimeOffset now);
}
