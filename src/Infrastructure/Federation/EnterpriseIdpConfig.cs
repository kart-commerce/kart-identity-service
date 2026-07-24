namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// One `EnterpriseIdps:{idpAlias}` configuration entry (appsettings/env vars) —
/// no specific enterprise IdP is named as already-integrated anywhere in the
/// design docs (Okta/Azure AD/Google Workspace are the BRD's illustrative
/// examples only), so this is config-driven per deployment, not hardcoded.
/// </summary>
public sealed class EnterpriseIdpConfig
{
    /// <summary>`saml` (default) or `oidc` — which federation protocol this idpAlias speaks.</summary>
    public string Protocol { get; init; } = "saml";

    /// <summary>The IdP's SAML 2.0 SSO (AuthnRequest destination) URL. SAML protocol only.</summary>
    public string SsoUrl { get; init; } = string.Empty;

    /// <summary>Identity's own SP entity ID, asserted as the AuthnRequest's `Issuer` and checked as the Assertion's `Audience`. SAML protocol only.</summary>
    public string SpEntityId { get; init; } = string.Empty;

    /// <summary>This IdP's dedicated ACS callback URL (api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs). SAML protocol only.</summary>
    public string AssertionConsumerServiceUrl { get; init; } = string.Empty;

    /// <summary>
    /// The IdP's own X.509 signing certificate (PEM). For SAML, verifies assertion
    /// signatures; for OIDC, verifies the id_token's JWS signature.
    /// </summary>
    public string SigningCertificatePem { get; init; } = string.Empty;

    /// <summary>The IdP's OIDC authorization endpoint (IDN-16's login redirect target). OIDC protocol only.</summary>
    public string AuthorizationEndpoint { get; init; } = string.Empty;

    /// <summary>The IdP's OIDC token endpoint (authorization-code exchange). OIDC protocol only.</summary>
    public string TokenEndpoint { get; init; } = string.Empty;

    /// <summary>Identity's registered OIDC client_id with this IdP. OIDC protocol only.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Identity's registered OIDC client_secret with this IdP. OIDC protocol only.</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>This IdP's dedicated OIDC callback URL (api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/oidc/callback). OIDC protocol only.</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>Expected id_token `iss` claim. OIDC protocol only.</summary>
    public string Issuer { get; init; } = string.Empty;
}
