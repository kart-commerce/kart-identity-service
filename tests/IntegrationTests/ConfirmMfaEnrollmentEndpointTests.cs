using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OtpNet;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/auth/mfa/enroll/confirm end to end over real HTTP.</summary>
public class ConfirmMfaEnrollmentEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string EnrollPath = "/v1/auth/mfa/enroll";
    private const string ConfirmPath = "/v1/auth/mfa/enroll/confirm";
    private readonly IdentityApiFactory _factory;

    public ConfirmMfaEnrollmentEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ConfirmMfaEnrollment_ValidCode_Returns200AndActivatesCredential()
    {
        var client = _factory.CreateClient();
        var (accessToken, email) = await RegisterAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var base32Secret = await EnrollAndGetSecretAsync(client);

        var response = await client.PostAsJsonAsync(ConfirmPath, new { totpCode = ComputeCurrentCode(base32Secret) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        var credential = await dbContext.MfaCredentials.SingleAsync(m => m.UserId == userId);
        Assert.Equal("Active", credential.Status.ToString());
        Assert.NotNull(credential.ConfirmedAt);
    }

    [Fact]
    public async Task ConfirmMfaEnrollment_WrongCode_Returns400()
    {
        var client = _factory.CreateClient();
        var (accessToken, _) = await RegisterAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await EnrollAndGetSecretAsync(client);

        var response = await client.PostAsJsonAsync(ConfirmPath, new { totpCode = "000000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmMfaEnrollment_NoPriorEnrollment_Returns400()
    {
        var client = _factory.CreateClient();
        var (accessToken, _) = await RegisterAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync(ConfirmPath, new { totpCode = "123456" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmMfaEnrollment_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(ConfirmPath, new { totpCode = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmMfaEnrollment_MalformedCode_Returns400()
    {
        var client = _factory.CreateClient();
        var (accessToken, _) = await RegisterAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await EnrollAndGetSecretAsync(client);

        var response = await client.PostAsJsonAsync(ConfirmPath, new { totpCode = "abc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<(string AccessToken, string Email)> RegisterAndGetAccessTokenAsync(HttpClient client)
    {
        var email = $"mfa-confirm-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (body.RootElement.GetProperty("accessToken").GetString()!, email);
    }

    private async Task<string> EnrollAndGetSecretAsync(HttpClient client)
    {
        var response = await client.PostAsync(EnrollPath, content: null);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var provisioningUri = body.RootElement.GetProperty("provisioningUri").GetString()!;
        return ExtractSecret(provisioningUri);
    }

    private static string ExtractSecret(string provisioningUri)
    {
        var query = new Uri(provisioningUri).Query.TrimStart('?');
        var secretParam = query.Split('&').Single(p => p.StartsWith("secret=", StringComparison.Ordinal));
        return secretParam["secret=".Length..];
    }

    private static string ComputeCurrentCode(string base32Secret) => new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();
}
