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

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Exercises api-contract.yaml POST /v1/auth/mfa/verify end to end over real
/// HTTP, completing the whole IDN-3 → IDN-4 → IDN-5 → IDN-6 chain: register,
/// grant Admin (no public endpoint exists — tests seed it directly, same as
/// LoginEndpointTests), enroll + confirm MFA, log in (gets a challenge), verify.
/// </summary>
public class VerifyMfaEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string LoginPath = "/v1/auth/login";
    private const string EnrollPath = "/v1/auth/mfa/enroll";
    private const string ConfirmPath = "/v1/auth/mfa/enroll/confirm";
    private const string VerifyPath = "/v1/auth/mfa/verify";
    private readonly IdentityApiFactory _factory;

    public VerifyMfaEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task VerifyMfa_ValidChallengeAndCode_Returns200WithTokenPair()
    {
        var client = _factory.CreateClient();
        var (email, password, base32Secret) = await RegisterAdminAndEnrollMfaAsync(client);

        var loginResponse = await client.PostAsJsonAsync(LoginPath, new { email, password });
        Assert.Equal(HttpStatusCode.Accepted, loginResponse.StatusCode);
        using var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var challengeId = loginBody.RootElement.GetProperty("challengeId").GetString();

        var response = await client.PostAsJsonAsync(VerifyPath, new { challengeId, totpCode = ComputeCurrentCode(base32Secret) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
        Assert.True(body.RootElement.TryGetProperty("refreshToken", out _));
        Assert.Contains("admin", body.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        // 2, not 1: registration itself already mints one session (Customer
        // default, before this test's out-of-band Admin grant) — Verify mints a
        // second, distinct one on top of it.
        Assert.Equal(2, await dbContext.Sessions.CountAsync(s => s.UserId == userId));
    }

    [Fact]
    public async Task VerifyMfa_PendingUnconfirmedEnrollment_ConfirmsCredentialAndReturns200()
    {
        // edge-cases.md dead-end: Login gates Admin/Support Agent on an MFA
        // challenge regardless of whether enrollment was ever confirmed, and
        // /mfa/enroll/confirm needs a bearer token the user can't get while
        // stuck on that challenge. Verify must accept a still-Pending
        // credential's first valid code so this isn't a permanent lockout.
        var client = _factory.CreateClient();
        const string password = "SuperSecret1";
        var email = $"mfa-pending-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterPath, new { email, password, displayName = "Admin User" });
        registerResponse.EnsureSuccessStatusCode();
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        dbContext.UserRoles.Add(UserRole.Grant(userId, PlatformRole.Admin, "test-seed", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var enrollResponse = await client.PostAsync(EnrollPath, content: null);
        enrollResponse.EnsureSuccessStatusCode();
        using var enrollBody = JsonDocument.Parse(await enrollResponse.Content.ReadAsStringAsync());
        var base32Secret = ExtractSecret(enrollBody.RootElement.GetProperty("provisioningUri").GetString()!);
        client.DefaultRequestHeaders.Authorization = null;
        // Enrollment is never confirmed here — this is the reported scenario.

        var loginResponse = await client.PostAsJsonAsync(LoginPath, new { email, password });
        Assert.Equal(HttpStatusCode.Accepted, loginResponse.StatusCode);
        using var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var challengeId = loginBody.RootElement.GetProperty("challengeId").GetString();

        var response = await client.PostAsJsonAsync(VerifyPath, new { challengeId, totpCode = ComputeCurrentCode(base32Secret) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var credential = await verifyDbContext.MfaCredentials.SingleAsync(c => c.UserId == userId);
        Assert.Equal(MfaCredentialStatus.Active, credential.Status);
    }

    [Fact]
    public async Task VerifyMfa_ChallengeAlreadyConsumed_Returns401OnSecondAttempt()
    {
        var client = _factory.CreateClient();
        var (email, password, base32Secret) = await RegisterAdminAndEnrollMfaAsync(client);
        var loginResponse = await client.PostAsJsonAsync(LoginPath, new { email, password });
        using var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var challengeId = loginBody.RootElement.GetProperty("challengeId").GetString();
        var totpCode = ComputeCurrentCode(base32Secret);

        var first = await client.PostAsJsonAsync(VerifyPath, new { challengeId, totpCode });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync(VerifyPath, new { challengeId, totpCode });

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task VerifyMfa_WrongCode_Returns401()
    {
        var client = _factory.CreateClient();
        var (email, password, _) = await RegisterAdminAndEnrollMfaAsync(client);
        var loginResponse = await client.PostAsJsonAsync(LoginPath, new { email, password });
        using var loginBody = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        var challengeId = loginBody.RootElement.GetProperty("challengeId").GetString();

        var response = await client.PostAsJsonAsync(VerifyPath, new { challengeId, totpCode = "000000" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyMfa_UnknownChallengeId_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(VerifyPath, new { challengeId = "does-not-exist", totpCode = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(string Email, string Password, string Base32Secret)> RegisterAdminAndEnrollMfaAsync(HttpClient client)
    {
        const string password = "SuperSecret1";
        var email = $"mfa-verify-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterPath, new { email, password, displayName = "Admin User" });
        registerResponse.EnsureSuccessStatusCode();
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        // No public role-elevation endpoint exists yet (database-design.md's
        // out-of-band note) — same reflection-free direct-seed precedent as
        // LoginCommandHandlerTests, but via the real DbContext over HTTP here.
        dbContext.UserRoles.Add(UserRole.Grant(userId, PlatformRole.Admin, "test-seed", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var enrollResponse = await client.PostAsync(EnrollPath, content: null);
        enrollResponse.EnsureSuccessStatusCode();
        using var enrollBody = JsonDocument.Parse(await enrollResponse.Content.ReadAsStringAsync());
        var base32Secret = ExtractSecret(enrollBody.RootElement.GetProperty("provisioningUri").GetString()!);

        var confirmResponse = await client.PostAsJsonAsync(ConfirmPath, new { totpCode = ComputeCurrentCode(base32Secret) });
        confirmResponse.EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = null;
        return (email, password, base32Secret);
    }

    private static string ExtractSecret(string provisioningUri)
    {
        var query = new Uri(provisioningUri).Query.TrimStart('?');
        var secretParam = query.Split('&').Single(p => p.StartsWith("secret=", StringComparison.Ordinal));
        return secretParam["secret=".Length..];
    }

    private static string ComputeCurrentCode(string base32Secret) => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();
}
