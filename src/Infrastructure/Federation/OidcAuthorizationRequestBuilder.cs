using System.Web;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// Builds a standard OIDC authorization-code request redirect (`response_type=code`)
/// — shared by enterprise OIDC federation (IDN-16) and customer social login (IDN-17).
/// </summary>
public sealed class OidcAuthorizationRequestBuilder : IOidcAuthorizationRequestBuilder
{
    public string BuildRedirectUrl(OidcProviderDescriptor provider, string state)
    {
        var query =
            $"response_type=code" +
            $"&client_id={HttpUtility.UrlEncode(provider.ClientId)}" +
            $"&redirect_uri={HttpUtility.UrlEncode(provider.RedirectUri)}" +
            $"&scope={HttpUtility.UrlEncode("openid email profile")}" +
            $"&state={HttpUtility.UrlEncode(state)}";

        var separator = provider.AuthorizationEndpoint.Contains('?') ? '&' : '?';
        return $"{provider.AuthorizationEndpoint}{separator}{query}";
    }
}
