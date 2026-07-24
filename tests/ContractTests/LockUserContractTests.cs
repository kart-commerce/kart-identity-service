using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies LockUser (IDN-10) against contracts/api-contract.yaml's POST
/// /internal/users/{userId}/lock path.
/// </summary>
public class LockUserContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/internal/users/{userId}/lock";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string TokenRequestPath = "/v1/auth/token";
    private const string ClientSecret = "CorrectSecret1";
    private readonly IdentityApiFactory _factory;

    public LockUserContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesLockPathWithClientCredentialsSecurity()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("lockUser", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("204"));
        Assert.True(responses.ContainsKey("403"));
        Assert.True(responses.ContainsKey("404"));
    }

    [Fact]
    public async Task LiveEndpoint_AdminScopedCaller_Returns204()
    {
        var client = _factory.CreateClient();
        var email = $"lock-contract-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterRequestPath, new { email, password = "SuperSecret1" });
        registerResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;

        var clientId = $"principal-{Guid.NewGuid():N}";
        var principal = ServicePrincipal.Provision(clientId, passwordHasher.Hash(ClientSecret), PlatformRole.Admin, DateTimeOffset.UtcNow, "test-seed");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();

        var tokenResponse = await client.PostAsync(TokenRequestPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = ClientSecret,
            ["scope"] = "admin"
        }));
        using var tokenBody = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
        var accessToken = tokenBody.RootElement.GetProperty("accessToken").GetString();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/internal/users/{userId}/lock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task LiveEndpoint_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/v1/internal/users/{Guid.NewGuid()}/lock", content: null);

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
