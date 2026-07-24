using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace Kart.Identity.Infrastructure.Federation;

/// <summary>Reads the `SocialIdps` configuration section, keyed by provider.</summary>
public sealed class ConfigSocialIdpDirectory(IOptions<Dictionary<string, SocialIdpConfig>> options) : ISocialIdpDirectory
{
    public OidcProviderDescriptor? Find(string provider)
    {
        if (!options.Value.TryGetValue(provider, out var config))
        {
            return null;
        }

        return new OidcProviderDescriptor(
            provider, config.AuthorizationEndpoint, config.TokenEndpoint, config.ClientId,
            config.ClientSecret, config.RedirectUri, config.Issuer, config.SigningCertificatePem);
    }
}
