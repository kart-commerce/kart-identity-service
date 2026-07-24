using System.Net;
using System.Net.Http.Json;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/auth/password/reset-initiate end to end over real HTTP.</summary>
public class InitiatePasswordResetEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string InitiatePath = "/v1/auth/password/reset-initiate";
    private readonly IdentityApiFactory _factory;

    public InitiatePasswordResetEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task InitiateReset_ExistingAccount_Returns202AndCreatesResetTokenRow()
    {
        var client = _factory.CreateClient();
        var email = $"reset-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        registerResponse.EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync(InitiatePath, new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        Assert.True(await dbContext.PasswordResetTokens.AnyAsync(t => t.UserId == userId));
    }

    [Fact]
    public async Task InitiateReset_UnknownAccount_Returns202AndCreatesNoResetTokenRow()
    {
        var client = _factory.CreateClient();
        var email = $"unknown-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(InitiatePath, new { email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.False(await dbContext.PasswordResetTokens.AnyAsync());
    }

    [Fact]
    public async Task InitiateReset_MalformedEmail_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(InitiatePath, new { email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
