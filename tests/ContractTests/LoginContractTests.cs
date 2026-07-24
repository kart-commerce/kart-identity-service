using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies Login (IDN-3) against contracts/api-contract.yaml's POST /auth/login
/// path — both that the contract still describes the shape this handler
/// implements, and that the live endpoint matches it.
/// </summary>
public class LoginContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/login";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string LoginRequestPath = "/v1/auth/login";
    private readonly IdentityApiFactory _factory;

    public LoginContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesLoginPathWithTokenPairAndMfaChallengeResponses()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("login", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        foreach (var expected in new[] { "200", "202", "401", "423", "429" })
        {
            Assert.True(responses.ContainsKey(expected), $"POST {ContractPath} no longer documents {expected}");
        }

        Assert.Equal("#/components/schemas/TokenPair", SchemaRef(responses, "200"));
        Assert.Equal("#/components/schemas/MfaChallenge", SchemaRef(responses, "202"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidCredentials_MatchesTokenPairShape()
    {
        var client = _factory.CreateClient();
        var email = $"login-contract-{Guid.NewGuid():N}@example.com";
        await client.PostAsJsonAsync(RegisterRequestPath, new { email, password = "SuperSecret1" });

        var response = await client.PostAsJsonAsync(LoginRequestPath, new { email, password = "SuperSecret1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var required in new[] { "accessToken", "refreshToken", "tokenType", "expiresIn" })
        {
            Assert.True(body.RootElement.TryGetProperty(required, out _), $"TokenPair response missing required '{required}'");
        }
    }

    [Fact]
    public async Task LiveEndpoint_InvalidCredentials_Returns401MatchingProblemShape()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginRequestPath, new
        {
            email = $"no-such-user-{Guid.NewGuid():N}@example.com",
            password = "WhateverPassword1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("code", out _));
        Assert.True(body.RootElement.TryGetProperty("message", out _));
    }

    private static string SchemaRef(Dictionary<object, object> responses, string statusCode)
    {
        var response = (Dictionary<object, object>)responses[statusCode];
        var schema = (Dictionary<object, object>)
            ((Dictionary<object, object>)((Dictionary<object, object>)response["content"])["application/json"])["schema"];
        return (string)schema["$ref"];
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
