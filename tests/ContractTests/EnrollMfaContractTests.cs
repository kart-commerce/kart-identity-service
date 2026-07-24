using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies EnrollMfa (IDN-4) against contracts/api-contract.yaml's
/// POST /auth/mfa/enroll path — both that the contract still describes the shape
/// this handler implements, and that the live endpoint matches it.
/// </summary>
public class EnrollMfaContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/mfa/enroll";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string EnrollRequestPath = "/v1/auth/mfa/enroll";
    private readonly IdentityApiFactory _factory;

    public EnrollMfaContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesEnrollMfaPathAsBearerAuthenticated()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("enrollMfa", postOp["operationId"]);
        Assert.True(postOp.ContainsKey("security"), $"POST {ContractPath} should require bearer auth");

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("401"));
    }

    [Fact]
    public async Task LiveEndpoint_Authenticated_MatchesDocumentedResponseShape()
    {
        var client = _factory.CreateClient();
        var email = $"mfa-contract-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterRequestPath, new { email, password = "SuperSecret1" });
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(EnrollRequestPath, content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("provisioningUri", out _));
        Assert.True(body.RootElement.TryGetProperty("secretExpiresAt", out _));
    }

    [Fact]
    public async Task LiveEndpoint_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(EnrollRequestPath, content: null);

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
