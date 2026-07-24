namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// One `SocialIdps:{provider}` configuration entry (appsettings/env vars) — no
/// specific social provider is named as already-integrated anywhere in the design
/// docs (Google/Apple are the BRD's illustrative examples only), so this is
/// config-driven per deployment, not hardcoded. Pure OIDC — social login has no
/// SAML option (requirement-spec.md §2).
/// </summary>
public sealed class SocialIdpConfig
{
    /// <summary>The provider's OIDC authorization endpoint (IDN-17's login redirect target).</summary>
    public string AuthorizationEndpoint { get; init; } = string.Empty;

    /// <summary>The provider's OIDC token endpoint (authorization-code exchange).</summary>
    public string TokenEndpoint { get; init; } = string.Empty;

    /// <summary>Identity's registered OIDC client_id with this provider.</summary>
    public string ClientId { get; init; } = string.Empty;

    /// <summary>Identity's registered OIDC client_secret with this provider.</summary>
    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>This provider's dedicated OIDC callback URL (api-contract.yaml GET /auth/sso/social/{provider}/callback).</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>Expected id_token `iss` claim.</summary>
    public string Issuer { get; init; } = string.Empty;

    /// <summary>The provider's own signing certificate (PEM), used to verify the id_token's JWS signature.</summary>
    public string SigningCertificatePem { get; init; } = string.Empty;
}
