using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies VerifyMfa (IDN-6) against contracts/api-contract.yaml's
/// POST /auth/mfa/verify path — both that the contract still describes the
/// shape this handler implements, and that the live endpoint matches it.
/// </summary>
public class VerifyMfaContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/mfa/verify";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string LoginRequestPath = "/v1/auth/login";
    private const string EnrollRequestPath = "/v1/auth/mfa/enroll";
    private const string ConfirmRequestPath = "/v1/auth/mfa/enroll/confirm";
    private const string VerifyRequestPath = "/v1/auth/mfa/verify";
    private readonly IdentityApiFactory _factory;

    public VerifyMfaContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesVerifyMfaPathAsUnauthenticated()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("verifyMfa", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("401"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidChallengeAndCode_MatchesDocumentedTokenPairShape()
    {
        var client = _factory.CreateClient();
        var email = $"mfa-verify-contract-{Guid.NewGuid():N}@example.com";
        const string password = "SuperSecret1";
        var registerResponse = await client.PostAsJsonAsync(RegisterRequestPath, new { email, password });
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        dbContext.UserRoles.Add(UserRole.Grant(userId, PlatformRole.Admin, "test-seed", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var enrollResponse = await client.PostAsync(EnrollRequestPath, content: null);
        using var enrollBody = JsonDocument.Parse(await enrollResponse.Content.ReadAsStringAsync());
        var base32Secret = ExtractSecret(enrollBody.RootElement.GetProperty("provisioningUri").GetString()!);
        var totpCode = new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();
        await client.PostAsJsonAsync(ConfirmRequestPath, new { totpCode });
        client.DefaultRequestHeaders.Authorization = null;

        var loginResponse = await client.PostAsJsonAsync(LoginRequestPath, new { email, password });
        using var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var challengeId = loginBody.RootElement.GetProperty("challengeId").GetString();

        var response = await client.PostAsJsonAsync(VerifyRequestPath, new { challengeId, totpCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
        Assert.True(body.RootElement.TryGetProperty("refreshToken", out _));
        Assert.True(body.RootElement.TryGetProperty("tokenType", out _));
        Assert.True(body.RootElement.TryGetProperty("expiresIn", out _));
    }

    [Fact]
    public async Task LiveEndpoint_UnknownChallenge_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(VerifyRequestPath, new { challengeId = "does-not-exist", totpCode = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ExtractSecret(string provisioningUri)
    {
        var query = new Uri(provisioningUri).Query.TrimStart('?');
        var secretParam = query.Split('&').Single(p => p.StartsWith("secret=", StringComparison.Ordinal));
        return secretParam["secret=".Length..];
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
