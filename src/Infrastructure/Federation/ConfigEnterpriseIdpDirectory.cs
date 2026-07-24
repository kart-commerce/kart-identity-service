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

        return new EnterpriseIdpDescriptor(idpAlias, config.SsoUrl, config.SpEntityId, config.AssertionConsumerServiceUrl, config.SigningCertificatePem);
    }
}
