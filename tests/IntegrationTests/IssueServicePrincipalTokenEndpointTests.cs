using System.Net;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Exercises api-contract.yaml POST /v1/auth/token end to end over real HTTP.
/// No public endpoint provisions `service_principals` (tickets.md's flagged
/// out-of-band-provisioning gap) — tests seed the row directly via the
/// DbContext, same precedent as Admin/Support Agent `user_roles` grants
/// elsewhere in this suite.
/// </summary>
public class IssueServicePrincipalTokenEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string TokenPath = "/v1/auth/token";
    private const string ClientSecret = "CorrectSecret1";
    private readonly IdentityApiFactory _factory;

    public IssueServicePrincipalTokenEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task IssueToken_ValidCredentials_Returns200WithAccessTokenAndNoRefreshToken()
    {
        var clientId = await SeedPrincipalAsync(PlatformRole.Admin, ServicePrincipalStatus.Active);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(TokenPath, FormBody(clientId, ClientSecret, "internal.lock"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.TryGetProperty("accessToken", out _));
        Assert.Equal("Bearer", body.RootElement.GetProperty("tokenType").GetString());
        Assert.False(body.RootElement.TryGetProperty("refreshToken", out _));
    }

    [Fact]
    public async Task IssueToken_WrongSecret_Returns401()
    {
        var clientId = await SeedPrincipalAsync(PlatformRole.Admin, ServicePrincipalStatus.Active);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(TokenPath, FormBody(clientId, "WrongSecret", null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IssueToken_UnsupportedGrantType_Returns400()
    {
        var clientId = await SeedPrincipalAsync(PlatformRole.Admin, ServicePrincipalStatus.Active);
        var client = _factory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = clientId,
            ["client_secret"] = ClientSecret
        });
        var response = await client.PostAsync(TokenPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task IssueToken_RevokedPrincipal_Returns401()
    {
        var clientId = await SeedPrincipalAsync(PlatformRole.PartnerApi, ServicePrincipalStatus.Revoked);
        var client = _factory.CreateClient();

        var response = await client.PostAsync(TokenPath, FormBody(clientId, ClientSecret, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<string> SeedPrincipalAsync(PlatformRole role, ServicePrincipalStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var clientId = $"principal-{Guid.NewGuid():N}";
        var principal = ServicePrincipal.Provision(clientId, passwordHasher.Hash(ClientSecret), role, DateTimeOffset.UtcNow, "test-seed");
        if (status == ServicePrincipalStatus.Revoked)
        {
            typeof(ServicePrincipal).GetProperty(nameof(ServicePrincipal.Status))!.SetValue(principal, ServicePrincipalStatus.Revoked);
        }

        dbContext.ServicePrincipals.Add(principal);
        await dbContext.SaveChangesAsync();
        return clientId;
    }

    private static FormUrlEncodedContent FormBody(string clientId, string clientSecret, string? scope)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };
        if (scope is not null)
        {
            fields["scope"] = scope;
        }

        return new FormUrlEncodedContent(fields);
    }
}
