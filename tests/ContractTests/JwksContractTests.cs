using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies GetJwks (IDN-1) against contracts/api-contract.yaml's
/// GET /.well-known/jwks.json path — both that the contract still describes
/// the shape this handler implements, and that the live endpoint matches it.
/// </summary>
public class JwksContractTests : IClassFixture<JwksApiFactory>
{
    private const string ContractPath = "/.well-known/jwks.json";
    private readonly JwksApiFactory _factory;

    public JwksContractTests(JwksApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesJwksPathWithKeysArrayResponse()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var getOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["get"];
        Assert.Equal("getJwks", getOp["operationId"]);

        var responses = (Dictionary<object, object>)getOp["responses"];
        var ok = (Dictionary<object, object>)responses["200"];
        var schema = (Dictionary<object, object>)
            ((Dictionary<object, object>)((Dictionary<object, object>)ok["content"])["application/json"])["schema"];
        var properties = (Dictionary<object, object>)schema["properties"];

        Assert.True(properties.ContainsKey("keys"), "api-contract.yaml's JWKS schema no longer has a 'keys' property");
        var keysProperty = (Dictionary<object, object>)properties["keys"];
        Assert.Equal("array", keysProperty["type"]);
    }

    [Fact]
    public async Task LiveEndpoint_MatchesContractShape()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(ContractPath);

        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var keys = body.RootElement.GetProperty("keys");
        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.True(keys.GetArrayLength() > 0, "expected at least the one configured RS256 signing key");
        Assert.Equal(JsonValueKind.Object, keys[0].ValueKind);
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
