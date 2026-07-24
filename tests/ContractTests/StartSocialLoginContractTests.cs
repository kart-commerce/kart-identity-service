using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies StartSocialLogin (IDN-17) against contracts/api-contract.yaml's
/// GET /auth/sso/social/{provider}/login path.
/// </summary>
public class StartSocialLoginContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/sso/social/{provider}/login";
    private readonly IdentityApiFactory _factory;

    public StartSocialLoginContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesLoginPathWith302And404()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var getOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["get"];
        Assert.Equal("startSocialLogin", getOp["operationId"]);

        var responses = (Dictionary<object, object>)getOp["responses"];
        Assert.True(responses.ContainsKey("302"));
        Assert.True(responses.ContainsKey("404"));
    }

    [Fact]
    public async Task LiveEndpoint_ConfiguredProvider_Returns302()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync($"/v1/auth/sso/social/{IdentityApiFactory.TestSocialProvider}/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task LiveEndpoint_UnknownProvider_Returns404()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/v1/auth/sso/social/no-such-provider/login");

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
