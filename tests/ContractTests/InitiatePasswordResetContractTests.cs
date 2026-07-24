using System.Net;
using System.Net.Http.Json;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies InitiatePasswordReset (IDN-12) against contracts/api-contract.yaml's
/// POST /auth/password/reset-initiate path.
/// </summary>
public class InitiatePasswordResetContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/password/reset-initiate";
    private const string InitiateRequestPath = "/v1/auth/password/reset-initiate";
    private readonly IdentityApiFactory _factory;

    public InitiatePasswordResetContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesResetInitiatePathWith202()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("initiatePasswordReset", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("202"));
    }

    [Fact]
    public async Task LiveEndpoint_AnyEmail_Returns202()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(InitiateRequestPath, new { email = $"contract-{Guid.NewGuid():N}@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
