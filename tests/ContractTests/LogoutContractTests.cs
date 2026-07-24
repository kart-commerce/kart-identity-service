using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies Logout (IDN-9) against contracts/api-contract.yaml's POST
/// /auth/logout path — both that the contract still describes the shape this
/// handler implements, and that the live endpoint matches it.
/// </summary>
public class LogoutContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/logout";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string LogoutRequestPath = "/v1/auth/logout";
    private readonly IdentityApiFactory _factory;

    public LogoutContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesLogoutPathWithBearerAuthAnd204()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("logout", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("204"));
        Assert.True(responses.ContainsKey("401"));
    }

    [Fact]
    public async Task LiveEndpoint_AuthenticatedNoBody_Returns204()
    {
        var client = _factory.CreateClient();
        var email = $"logout-contract-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterRequestPath, new { email, password = "SuperSecret1" });
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(LogoutRequestPath, content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task LiveEndpoint_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(LogoutRequestPath, content: null);

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
