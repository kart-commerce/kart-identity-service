using System.Net;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies EnterpriseSamlAssertionConsumer (IDN-15) against
/// contracts/api-contract.yaml's POST /auth/sso/enterprise/{idpAlias}/saml/acs
/// path.
/// </summary>
public class EnterpriseSamlAssertionConsumerContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/sso/enterprise/{idpAlias}/saml/acs";
    private readonly IdentityApiFactory _factory;

    public EnterpriseSamlAssertionConsumerContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesAcsPathWith200And401And409()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("enterpriseSamlAssertionConsumer", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("401"));
        Assert.True(responses.ContainsKey("409"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidSignedAssertion_MatchesDocumentedTokenPairShape()
    {
        var client = _factory.CreateClient();
        var samlResponse = TestSamlResponseBuilder.BuildSignedResponse(
            _factory.TestIdpCertificate, IdentityApiFactory.TestIdpSpEntityId, $"contract-{Guid.NewGuid():N}@example.com", [], DateTimeOffset.UtcNow);
        var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["SAMLResponse"] = samlResponse });

        var response = await client.PostAsync($"/v1/auth/sso/enterprise/{IdentityApiFactory.TestIdpAlias}/saml/acs", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
        Assert.True(body.RootElement.TryGetProperty("refreshToken", out _));
        Assert.True(body.RootElement.TryGetProperty("tokenType", out _));
        Assert.True(body.RootElement.TryGetProperty("expiresIn", out _));
    }

    [Fact]
    public async Task LiveEndpoint_InvalidAssertion_Returns401()
    {
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["SAMLResponse"] = "bm90LWEtdmFsaWQtc2FtbC1yZXNwb25zZQ==" });

        var response = await client.PostAsync($"/v1/auth/sso/enterprise/{IdentityApiFactory.TestIdpAlias}/saml/acs", content);

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
