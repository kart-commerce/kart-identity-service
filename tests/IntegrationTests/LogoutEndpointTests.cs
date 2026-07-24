using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/auth/logout end to end over real HTTP.</summary>
public class LogoutEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string LogoutPath = "/v1/auth/logout";
    private const string RefreshPath = "/v1/auth/refresh";
    private readonly IdentityApiFactory _factory;

    public LogoutEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Logout_NoBody_Returns204()
    {
        var client = _factory.CreateClient();
        var (accessToken, _, _) = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsync(LogoutPath, content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync(LogoutPath, content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithRefreshToken_RevokesSessionSoRefreshNoLongerWorks()
    {
        var client = _factory.CreateClient();
        var (accessToken, refreshToken, email) = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PostAsJsonAsync(LogoutPath, new { refreshToken });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        var session = await dbContext.Sessions.SingleAsync(s => s.UserId == userId);
        Assert.NotNull(session.RevokedAt);

        var refreshAfterLogout = await client.PostAsJsonAsync(RefreshPath, new { refreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    private async Task<(string AccessToken, string RefreshToken, string Email)> RegisterAsync(HttpClient client)
    {
        var email = $"logout-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (
            body.RootElement.GetProperty("accessToken").GetString()!,
            body.RootElement.GetProperty("refreshToken").GetString()!,
            email);
    }
}
