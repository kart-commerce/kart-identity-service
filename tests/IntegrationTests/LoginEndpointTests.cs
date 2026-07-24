using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/auth/login end to end over real HTTP.</summary>
public class LoginEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string LoginPath = "/v1/auth/login";
    private const string Password = "SuperSecret1";
    private readonly IdentityApiFactory _factory;

    public LoginEndpointTests(IdentityApiFactory factory)
    {
        _factory = factory;

        // The in-memory login-throttle test double is a singleton for the whole
        // class fixture, and every in-process TestServer request shares one
        // synthetic IP — reset before each test so throttle state from one test
        // can't leak into another (xUnit constructs a new test-class instance,
        // and so re-runs this constructor, per test method).
        var throttle = (InMemoryLoginAttemptThrottle)_factory.Services.GetRequiredService<ILoginAttemptThrottle>();
        throttle.ClearAll();
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokenPair()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("refreshToken").GetString()));
        Assert.Equal("customer", root.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401Problem()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_credentials", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401Problem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginPath, new { email = UniqueEmail(), password = Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_TooManyFailedAttempts_Returns429Problem()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAsync(client);

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            lastResponse = await client.PostAsJsonAsync(LoginPath, new { email, password = "WrongPassword1" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
        using var body = JsonDocument.Parse(await lastResponse.Content.ReadAsStringAsync());
        Assert.Equal("rate_limited", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_LockedAccount_Returns423Problem()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAsync(client);
        await LockUserAsync(email);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("account_locked", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_AdminRole_Returns202WithMfaChallenge()
    {
        var client = _factory.CreateClient();
        var email = await RegisterAsync(client);
        await GrantAdminRoleAsync(email);

        var response = await client.PostAsJsonAsync(LoginPath, new { email, password = Password });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(body.RootElement.GetProperty("challengeId").GetString()));
    }

    private async Task<string> RegisterAsync(HttpClient client)
    {
        var email = UniqueEmail();
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = Password, displayName = "Test User" });
        response.EnsureSuccessStatusCode();
        return email;
    }

    private async Task LockUserAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Email == email);
        // No admin-lock endpoint exists yet (IDN-10) — reflection is the only way
        // to put a row into the locked state Login must already defend against.
        typeof(User).GetProperty(nameof(User.LockedAt))!.SetValue(user, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    private async Task GrantAdminRoleAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Email == email);
        dbContext.UserRoles.Add(UserRole.Grant(user.UserId, PlatformRole.Admin, "test-seed", DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();
    }

    private static string UniqueEmail() => $"login-{Guid.NewGuid():N}@example.com";
}
