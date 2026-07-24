using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure — builds the SP-initiated
/// SAML 2.0 AuthnRequest redirect URL (HTTP-Redirect binding) for
/// api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/login.
/// </summary>
public interface ISamlAuthnRequestBuilder
{
    string BuildRedirectUrl(EnterpriseIdpDescriptor idp, DateTimeOffset now);
}
