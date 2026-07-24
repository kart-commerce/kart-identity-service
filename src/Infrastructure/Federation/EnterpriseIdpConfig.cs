namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// One `EnterpriseIdps:{idpAlias}` configuration entry (appsettings/env vars) —
/// no specific enterprise IdP is named as already-integrated anywhere in the
/// design docs (Okta/Azure AD/Google Workspace are the BRD's illustrative
/// examples only), so this is config-driven per deployment, not hardcoded.
/// </summary>
public sealed class EnterpriseIdpConfig
{
    /// <summary>The IdP's SAML 2.0 SSO (AuthnRequest destination) URL.</summary>
    public string SsoUrl { get; init; } = string.Empty;

    /// <summary>Identity's own SP entity ID, asserted as the AuthnRequest's `Issuer` and checked as the Assertion's `Audience`.</summary>
    public string SpEntityId { get; init; } = string.Empty;

    /// <summary>This IdP's dedicated ACS callback URL (api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs).</summary>
    public string AssertionConsumerServiceUrl { get; init; } = string.Empty;

    /// <summary>The IdP's own X.509 signing certificate (PEM), used to verify assertion signatures.</summary>
    public string SigningCertificatePem { get; init; } = string.Empty;
}
