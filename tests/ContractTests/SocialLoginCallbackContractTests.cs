using System.Net;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies SocialLoginCallback (IDN-18) against contracts/api-contract.yaml's
/// GET /auth/sso/social/{provider}/callback path.
/// </summary>
public class SocialLoginCallbackContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/sso/social/{provider}/callback";
    private readonly IdentityApiFactory _factory;

    public SocialLoginCallbackContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesCallbackPathWith200And401()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var getOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["get"];
        Assert.Equal("socialLoginCallback", getOp["operationId"]);

        var responses = (Dictionary<object, object>)getOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("401"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidCode_MatchesDocumentedTokenPairShape()
    {
        var client = _factory.CreateClient();
        var code = TestOidcCode.Encode($"contract-{Guid.NewGuid():N}");

        var response = await client.GetAsync($"/v1/auth/sso/social/{IdentityApiFactory.TestSocialProvider}/callback?code={code}&state=opaque-state");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
        Assert.True(body.RootElement.TryGetProperty("refreshToken", out _));
        Assert.True(body.RootElement.TryGetProperty("tokenType", out _));
        Assert.True(body.RootElement.TryGetProperty("expiresIn", out _));
    }

    [Fact]
    public async Task LiveEndpoint_InvalidCode_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/v1/auth/sso/social/{IdentityApiFactory.TestSocialProvider}/callback?code=invalid-code&state=opaque-state");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
