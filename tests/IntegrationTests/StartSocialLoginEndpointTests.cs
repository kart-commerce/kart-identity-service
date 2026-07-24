using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml GET /v1/auth/sso/social/{provider}/login end to end over real HTTP.</summary>
public class StartSocialLoginEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public StartSocialLoginEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task StartSocialLogin_ConfiguredProvider_RedirectsToProviderAuthorizationEndpoint()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/v1/auth/sso/social/{IdentityApiFactory.TestSocialProvider}/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(IdentityApiFactory.TestSocialAuthorizationEndpoint, response.Headers.Location!.ToString());
        Assert.Contains("response_type=code", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task StartSocialLogin_UnknownProvider_Returns404()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/v1/auth/sso/social/no-such-provider/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
