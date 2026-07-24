using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies UpdateProfile (IDN-19) against contracts/api-contract.yaml's
/// PATCH /auth/profile path.
/// </summary>
public class UpdateProfileContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/profile";
    private readonly IdentityApiFactory _factory;

    public UpdateProfileContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesProfilePathWith200And401And409And400()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var patchOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["patch"];
        Assert.Equal("updateProfile", patchOp["operationId"]);

        var responses = (Dictionary<object, object>)patchOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("401"));
        Assert.True(responses.ContainsKey("409"));
        Assert.True(responses.ContainsKey("400"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidUpdate_MatchesDocumentedResponseShape()
    {
        var client = _factory.CreateClient();
        var email = $"contract-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync("/v1/auth/register", new { email, password = "SuperSecret1", displayName = "Test User" });
        registerResponse.EnsureSuccessStatusCode();
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PatchAsJsonAsync("/v1/auth/profile", new { displayName = "Contract Name" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("email", out _));
        Assert.True(body.RootElement.TryGetProperty("displayName", out _));
        Assert.True(body.RootElement.TryGetProperty("updatedAt", out _));
    }

    [Fact]
    public async Task LiveEndpoint_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync("/v1/auth/profile", new { displayName = "Name" });

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
