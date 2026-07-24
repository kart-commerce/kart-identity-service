namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// Which federation protocol a configured enterprise IdP speaks — api-contract.yaml
/// GET /auth/sso/enterprise/{idpAlias}/login's own description states idpAlias
/// redirects to "the enterprise IdP's SAML AuthnRequest or OIDC authorization
/// endpoint," i.e. one idpAlias is exactly one protocol, decided by configuration.
/// </summary>
public enum EnterpriseIdpProtocol
{
    Saml,
    Oidc
}
