using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/auth/refresh end to end over real HTTP.</summary>
public class RotateRefreshTokenEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string RefreshPath = "/v1/auth/refresh";
    private readonly IdentityApiFactory _factory;

    public RotateRefreshTokenEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithRotatedPair()
    {
        var client = _factory.CreateClient();
        var (_, refreshToken) = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(RefreshPath, new { refreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var rotatedToken = body.RootElement.GetProperty("refreshToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("accessToken").GetString()));
        Assert.NotEqual(refreshToken, rotatedToken);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(RefreshPath, new { refreshToken = "does-not-exist" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("refresh_token_reuse_detected", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Refresh_ReplayedAlreadyRotatedToken_Returns401AndKillsTheWholeSession()
    {
        var client = _factory.CreateClient();
        var (email, originalToken) = await RegisterAsync(client);

        var firstRefresh = await client.PostAsJsonAsync(RefreshPath, new { refreshToken = originalToken });
        firstRefresh.EnsureSuccessStatusCode();
        using var firstBody = JsonDocument.Parse(await firstRefresh.Content.ReadAsStringAsync());
        var rotatedToken = firstBody.RootElement.GetProperty("refreshToken").GetString();

        // Replaying the now-stale original token — edge-cases.md, "Refresh Token
        // Replay After Rotation."
        var replay = await client.PostAsJsonAsync(RefreshPath, new { refreshToken = originalToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The whole family (== the session) is now dead — even the legitimately
        // rotated, never-replayed token from the first refresh no longer works.
        var afterFamilyKill = await client.PostAsJsonAsync(RefreshPath, new { refreshToken = rotatedToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterFamilyKill.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        var session = await dbContext.Sessions.SingleAsync(s => s.UserId == userId);
        Assert.NotNull(session.RevokedAt);
    }

    private async Task<(string Email, string RefreshToken)> RegisterAsync(HttpClient client)
    {
        var email = $"refresh-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (email, body.RootElement.GetProperty("refreshToken").GetString()!);
    }
}
