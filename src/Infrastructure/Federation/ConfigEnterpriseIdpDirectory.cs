using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace Kart.Identity.Infrastructure.Federation;

/// <summary>Reads the `EnterpriseIdps` configuration section, keyed by idpAlias.</summary>
public sealed class ConfigEnterpriseIdpDirectory(IOptions<Dictionary<string, EnterpriseIdpConfig>> options) : IEnterpriseIdpDirectory
{
    public EnterpriseIdpDescriptor? Find(string idpAlias)
    {
        if (!options.Value.TryGetValue(idpAlias, out var config))
        {
            return null;
        }

        var protocol = string.Equals(config.Protocol, "oidc", StringComparison.OrdinalIgnoreCase)
            ? EnterpriseIdpProtocol.Oidc
            : EnterpriseIdpProtocol.Saml;

        var oidc = protocol == EnterpriseIdpProtocol.Oidc
            ? new OidcProviderDescriptor(
                idpAlias, config.AuthorizationEndpoint, config.TokenEndpoint, config.ClientId,
                config.ClientSecret, config.RedirectUri, config.Issuer, config.SigningCertificatePem)
            : null;

        return new EnterpriseIdpDescriptor(
            idpAlias, config.SsoUrl, config.SpEntityId, config.AssertionConsumerServiceUrl, config.SigningCertificatePem,
            protocol, oidc);
    }
}
