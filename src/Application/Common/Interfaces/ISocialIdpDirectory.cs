using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure against configuration —
/// the config-driven registry of customer social-login OIDC providers this
/// instance is set up to authenticate against (api-contract.yaml's `{provider}`
/// path parameter, e.g. `google`/`apple`). The social-login equivalent of
/// <see cref="IEnterpriseIdpDirectory"/> — no SAML option and no group-mapping
/// concept, since social login always resolves to `Customer` only
/// (requirement-spec.md §2, resolved Open Question #7).
/// </summary>
public interface ISocialIdpDirectory
{
    /// <summary>Null if <paramref name="provider"/> is not configured (api-contract.yaml's 404 for the login-redirect endpoint).</summary>
    OidcProviderDescriptor? Find(string provider);
}
