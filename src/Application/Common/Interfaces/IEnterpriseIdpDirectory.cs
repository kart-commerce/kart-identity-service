using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure against configuration —
/// the config-driven registry of enterprise IdPs this instance is set up to
/// federate with (api-contract.yaml's `{idpAlias}` path parameter).
/// </summary>
public interface IEnterpriseIdpDirectory
{
    /// <summary>Null if <paramref name="idpAlias"/> is not configured (api-contract.yaml's 404 for the login-redirect endpoint).</summary>
    EnterpriseIdpDescriptor? Find(string idpAlias);
}
