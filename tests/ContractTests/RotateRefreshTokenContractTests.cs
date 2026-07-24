using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies RotateRefreshToken (IDN-8) against contracts/api-contract.yaml's
/// POST /auth/refresh path — both that the contract still describes the shape
/// this handler implements, and that the live endpoint matches it.
/// </summary>
public class RotateRefreshTokenContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/refresh";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string RefreshRequestPath = "/v1/auth/refresh";
    private readonly IdentityApiFactory _factory;

    public RotateRefreshTokenContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesRefreshPathWithReuseAndRaceResponses()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("refreshToken", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("401"));
        Assert.True(responses.ContainsKey("409"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidToken_MatchesDocumentedTokenPairShape()
    {
        var client = _factory.CreateClient();
        var email = $"refresh-contract-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterRequestPath, new { email, password = "SuperSecret1" });
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var refreshToken = registerBody.RootElement.GetProperty("refreshToken").GetString();

        var response = await client.PostAsJsonAsync(RefreshRequestPath, new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
        Assert.True(body.RootElement.TryGetProperty("refreshToken", out _));
        Assert.True(body.RootElement.TryGetProperty("tokenType", out _));
        Assert.True(body.RootElement.TryGetProperty("expiresIn", out _));
    }

    [Fact]
    public async Task LiveEndpoint_UnknownToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(RefreshRequestPath, new { refreshToken = "does-not-exist" });

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
