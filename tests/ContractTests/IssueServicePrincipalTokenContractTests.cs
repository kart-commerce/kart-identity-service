using System.Net;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies IssueServicePrincipalToken (IDN-7) against contracts/api-contract.yaml's
/// POST /auth/token path — both that the contract still describes the shape this
/// handler implements, and that the live endpoint matches it.
/// </summary>
public class IssueServicePrincipalTokenContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/token";
    private const string TokenRequestPath = "/v1/auth/token";
    private const string ClientSecret = "CorrectSecret1";
    private readonly IdentityApiFactory _factory;

    public IssueServicePrincipalTokenContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesTokenPathAcceptingClientCredentialsGrant()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("issueServicePrincipalToken", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("401"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidCredentials_MatchesDocumentedServicePrincipalTokenShape()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var clientId = $"principal-contract-{Guid.NewGuid():N}";
        dbContext.ServicePrincipals.Add(ServicePrincipal.Provision(
            clientId, passwordHasher.Hash(ClientSecret), PlatformRole.Admin, DateTimeOffset.UtcNow, "test-seed"));
        await dbContext.SaveChangesAsync();

        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = ClientSecret
        });

        var response = await client.PostAsync(TokenRequestPath, content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
        Assert.True(body.RootElement.TryGetProperty("tokenType", out _));
        Assert.True(body.RootElement.TryGetProperty("expiresIn", out _));
    }

    [Fact]
    public async Task LiveEndpoint_InvalidCredentials_Returns401()
    {
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "no-such-client",
            ["client_secret"] = "whatever"
        });

        var response = await client.PostAsync(TokenRequestPath, content);

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
