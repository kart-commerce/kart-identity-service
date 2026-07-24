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

/// <summary>
/// Exercises api-contract.yaml POST /v1/internal/users/{userId}/lock end to end
/// over real HTTP. No public endpoint provisions `service_principals` (tickets.md's
/// flagged out-of-band-provisioning gap) — tests seed the row directly, same
/// precedent as IssueServicePrincipalTokenEndpointTests.
/// </summary>
public class LockUserEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string TokenPath = "/v1/auth/token";
    private const string ClientSecret = "CorrectSecret1";
    private readonly IdentityApiFactory _factory;

    public LockUserEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Lock_AdminScopedCaller_Returns204AndLocksUserAndRevokesLiveSessions()
    {
        var client = _factory.CreateClient();
        var (userId, email) = await RegisterAsync(client);
        var adminToken = await IssueAdminScopedTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/internal/users/{userId}/lock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var lockedUser = await dbContext.Users.SingleAsync(u => u.Email == email);
        Assert.NotNull(lockedUser.LockedAt);
        var session = await dbContext.Sessions.SingleAsync(s => s.UserId == lockedUser.UserId);
        Assert.Equal(SessionRevocationReason.AdminLock, session.RevokedReason);
    }

    [Fact]
    public async Task Lock_CallerWithoutAdminScope_Returns403()
    {
        var client = _factory.CreateClient();
        var (userId, _) = await RegisterAsync(client);
        var nonAdminToken = await IssuePartnerScopedTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/internal/users/{userId}/lock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", nonAdminToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Lock_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync($"/v1/internal/users/{Guid.NewGuid()}/lock", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lock_UnknownUserId_Returns404()
    {
        var client = _factory.CreateClient();
        var adminToken = await IssueAdminScopedTokenAsync(client);

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/internal/users/{Guid.NewGuid()}/lock");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(Guid UserId, string Email)> RegisterAsync(HttpClient client)
    {
        var email = $"lock-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        response.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        return (userId, email);
    }

    private async Task<string> IssueAdminScopedTokenAsync(HttpClient client) =>
        await IssueTokenAsync(client, PlatformRole.Admin, "admin");

    private async Task<string> IssuePartnerScopedTokenAsync(HttpClient client) =>
        await IssueTokenAsync(client, PlatformRole.PartnerApi, "partner");

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
