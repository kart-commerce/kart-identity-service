using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies StartEnterpriseFederation (IDN-14) against contracts/api-contract.yaml's
/// GET /auth/sso/enterprise/{idpAlias}/login path.
/// </summary>
public class StartEnterpriseFederationContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/sso/enterprise/{idpAlias}/login";
    private readonly IdentityApiFactory _factory;

    public StartEnterpriseFederationContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesLoginPathWith302And404()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var getOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["get"];
        Assert.Equal("startEnterpriseFederation", getOp["operationId"]);

        var responses = (Dictionary<object, object>)getOp["responses"];
        Assert.True(responses.ContainsKey("302"));
        Assert.True(responses.ContainsKey("404"));
    }

    [Fact]
    public async Task LiveEndpoint_ConfiguredIdp_Returns302()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/v1/auth/sso/enterprise/{IdentityApiFactory.TestIdpAlias}/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task LiveEndpoint_UnknownIdpAlias_Returns404()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/v1/auth/sso/enterprise/no-such-idp/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
