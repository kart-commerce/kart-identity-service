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

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/internal/users/{userId}/unlock end to end over real HTTP.</summary>
public class UnlockUserEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string TokenPath = "/v1/auth/token";
    private const string ClientSecret = "CorrectSecret1";
    private readonly IdentityApiFactory _factory;

    public UnlockUserEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Unlock_AdminScopedCaller_Returns204AndClearsLock()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await RegisterAndLockAsync(client);
        var adminToken = await IssueAdminScopedTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/internal/users/{userId}/unlock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var unlockedUser = await dbContext.Users.SingleAsync(u => u.Email == email);
        Assert.Null(unlockedUser.LockedAt);
    }

    [Fact]
    public async Task Unlock_CallerWithoutAdminScope_Returns403()
    {
        var client = _factory.CreateClient();
        var (userId, _) = await RegisterAndLockAsync(client);
        var nonAdminToken = await IssueTokenAsync(client, PlatformRole.PartnerApi, "partner");

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/internal/users/{userId}/unlock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nonAdminToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Unlock_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/v1/internal/users/{Guid.NewGuid()}/unlock", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unlock_UnknownUserId_Returns404()
    {
        var client = _factory.CreateClient();
        var adminToken = await IssueAdminScopedTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/internal/users/{Guid.NewGuid()}/unlock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(Guid UserId, string Email)> RegisterAndLockAsync(HttpClient client)
    {
        var email = $"unlock-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Email == email);
        user.Lock(DateTimeOffset.UtcNow, "some-admin");
        await dbContext.SaveChangesAsync();

        return (user.UserId, email);
    }

    private async Task<string> IssueAdminScopedTokenAsync(HttpClient client) =>
        await IssueTokenAsync(client, PlatformRole.Admin, "admin");

    private async Task<string> IssueTokenAsync(HttpClient client, PlatformRole role, string scope)
    {
        using var scopeContainer = _factory.Services.CreateScope();
        var dbContext = scopeContainer.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scopeContainer.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var clientId = $"principal-{Guid.NewGuid():N}";
        var principal = ServicePrincipal.Provision(clientId, passwordHasher.Hash(ClientSecret), role, DateTimeOffset.UtcNow, "test-seed");
        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = ClientSecret,
            ["scope"] = scope
        });
        var response = await client.PostAsync(TokenPath, content);
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("accessToken").GetString()!;
    }
}
