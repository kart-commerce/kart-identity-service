using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies RegisterUser (IDN-2) against contracts/api-contract.yaml's
/// POST /auth/register path — both that the contract still describes the shape
/// this handler implements, and that the live endpoint matches it.
/// </summary>
public class RegisterContractTests : IClassFixture<RegisterApiFactory>
{
    private const string ContractPath = "/auth/register";
    private const string RequestPath = "/v1/auth/register";
    private readonly RegisterApiFactory _factory;

    public RegisterContractTests(RegisterApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesRegisterPathWithTokenPairResponse()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("registerUser", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("201"));
        Assert.True(responses.ContainsKey("409"));
        Assert.True(responses.ContainsKey("400"));

        var created = (Dictionary<object, object>)responses["201"];
        var schemaRef = (string)((Dictionary<object, object>)
            ((Dictionary<object, object>)((Dictionary<object, object>)created["content"])["application/json"])["schema"])["$ref"];
        Assert.Equal("#/components/schemas/TokenPair", schemaRef);
    }

    [Fact]
    public async Task LiveEndpoint_SuccessfulRegistration_MatchesTokenPairShape()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(RequestPath, new
        {
            email = $"contract-{Guid.NewGuid():N}@example.com",
            password = "SuperSecret1",
            displayName = "Contract Test"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        foreach (var required in new[] { "accessToken", "refreshToken", "tokenType", "expiresIn" })
        {
            Assert.True(root.TryGetProperty(required, out _), $"TokenPair response missing required '{required}'");
        }
    }

    [Fact]
    public async Task LiveEndpoint_DuplicateEmail_Returns409MatchingProblemShape()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            email = $"contract-dup-{Guid.NewGuid():N}@example.com",
            password = "SuperSecret1"
        };

        await client.PostAsJsonAsync(RequestPath, payload);
        var response = await client.PostAsJsonAsync(RequestPath, payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("code", out _));
        Assert.True(body.RootElement.TryGetProperty("message", out _));
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
