using System.Web;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Infrastructure.Federation;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Federation;

public class OidcAuthorizationRequestBuilderTests
{
    private readonly OidcAuthorizationRequestBuilder _builder = new();

    [Fact]
    public void BuildRedirectUrl_ProducesAuthorizationCodeRequestTargetingProviderEndpoint()
    {
        var provider = new OidcProviderDescriptor(
            "azure-ad", "https://login.example.com/authorize", "https://login.example.com/token",
            "client-id", "client-secret", "https://identity.example.com/oidc/callback", "https://login.example.com", "cert-pem");

        var redirectUrl = _builder.BuildRedirectUrl(provider, "opaque-state");

        Assert.StartsWith("https://login.example.com/authorize?", redirectUrl);

        var uri = new Uri(redirectUrl);
        var query = HttpUtility.ParseQueryString(uri.Query);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("client-id", query["client_id"]);
        Assert.Equal("https://identity.example.com/oidc/callback", query["redirect_uri"]);
        Assert.Equal("openid email profile", query["scope"]);
        Assert.Equal("opaque-state", query["state"]);
    }

    [Fact]
    public void BuildRedirectUrl_AuthorizationEndpointAlreadyHasQuery_AppendsWithAmpersand()
    {
        var provider = new OidcProviderDescriptor(
            "azure-ad", "https://login.example.com/authorize?tenant=acme", "https://login.example.com/token",
            "client-id", "client-secret", "https://identity.example.com/oidc/callback", "https://login.example.com", "cert-pem");

        var redirectUrl = _builder.BuildRedirectUrl(provider, "opaque-state");

        Assert.StartsWith("https://login.example.com/authorize?tenant=acme&", redirectUrl);
    }
}
