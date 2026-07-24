using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/auth/mfa/enroll end to end over real HTTP.</summary>
public class EnrollMfaEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string EnrollPath = "/v1/auth/mfa/enroll";
    private readonly IdentityApiFactory _factory;

    public EnrollMfaEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task EnrollMfa_Authenticated_Returns200WithProvisioningUri()
    {
        var client = _factory.CreateClient();
        var (accessToken, _) = await RegisterAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(EnrollPath, content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var provisioningUri = body.RootElement.GetProperty("provisioningUri").GetString();
        Assert.StartsWith("otpauth://totp/", provisioningUri);
        Assert.True(body.RootElement.TryGetProperty("secretExpiresAt", out _));
    }

    [Fact]
    public async Task EnrollMfa_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(EnrollPath, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task EnrollMfa_CalledTwice_ReplacesCredentialRatherThanAddingASecondOne()
    {
        var client = _factory.CreateClient();
        var (accessToken, email) = await RegisterAndGetAccessTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var first = await client.PostAsync(EnrollPath, content: null);
        var second = await client.PostAsync(EnrollPath, content: null);

        using var firstBody = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondBody = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.NotEqual(
            firstBody.RootElement.GetProperty("provisioningUri").GetString(),
            secondBody.RootElement.GetProperty("provisioningUri").GetString());

        // The Sqlite DB is shared across every test method in this class fixture,
        // so the count must be scoped to this test's own user, not the whole
        // table (other tests' enrollments live in the same table).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        Assert.Equal(1, await dbContext.MfaCredentials.CountAsync(m => m.UserId == userId));
    }

    private async Task<(string AccessToken, string Email)> RegisterAndGetAccessTokenAsync(HttpClient client)
    {
        var email = $"mfa-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (body.RootElement.GetProperty("accessToken").GetString()!, email);
    }
}
