using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml GET /v1/auth/sso/enterprise/{idpAlias}/login end to end over real HTTP.</summary>
public class StartEnterpriseFederationEndpointTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public StartEnterpriseFederationEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task StartFederation_ConfiguredIdp_RedirectsToIdpSsoUrl()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/v1/auth/sso/enterprise/{IdentityApiFactory.TestIdpAlias}/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(IdentityApiFactory.TestIdpSsoUrl, response.Headers.Location!.ToString());
        Assert.Contains("SAMLRequest=", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task StartFederation_UnknownIdpAlias_Returns404()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/v1/auth/sso/enterprise/no-such-idp/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
