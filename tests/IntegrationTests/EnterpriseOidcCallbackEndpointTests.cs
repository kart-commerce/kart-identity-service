using System.Net;
using System.Net.Http.Json;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Exercises api-contract.yaml GET /v1/auth/sso/enterprise/{idpAlias}/oidc/callback
/// end to end over real HTTP, with <see cref="FakeOidcTokenEndpointHandler"/>
/// standing in for the enterprise IdP's real token endpoint.
/// </summary>
public class EnterpriseOidcCallbackEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string CallbackPathTemplate = "/v1/auth/sso/enterprise/{0}/oidc/callback?code={1}&state=opaque-state";
    private readonly IdentityApiFactory _factory;

    public EnterpriseOidcCallbackEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Callback_FirstLoginForExternalIdentity_Returns200AndJitProvisionsAccount()
    {
        var client = _factory.CreateClient();
        var subject = $"alice-{Guid.NewGuid():N}";
        var code = TestOidcCode.Encode(subject, $"{subject}@example.com");

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, IdentityApiFactory.TestOidcIdpAlias, code));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenPairDto>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.Empty(body.Roles);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var federatedIdentity = await dbContext.FederatedIdentities.SingleAsync(f => f.ExternalSubjectId == subject);
        Assert.Equal(FederatedIdpType.Enterprise, federatedIdentity.IdpType);
        var session = await dbContext.Sessions.SingleAsync(s => s.UserId == federatedIdentity.UserId);
        Assert.True(session.IsFederated);
    }

    [Fact]
    public async Task Callback_MappedGroup_ReturnsMappedRole()
    {
        var subject = $"bob-{Guid.NewGuid():N}";
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            dbContext.IdpGroupRoleMappings.Add(
                IdpGroupRoleMapping.Create(IdentityApiFactory.TestOidcIdpAlias, "Engineering", PlatformRole.SupportAgent, DateTimeOffset.UtcNow, "operator"));
            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var code = TestOidcCode.Encode(subject, groups: ["Engineering"]);

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, IdentityApiFactory.TestOidcIdpAlias, code));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenPairDto>();
        Assert.Equal(["support_agent"], body!.Roles);
    }

    [Fact]
    public async Task Callback_TokenExchangeFails_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, IdentityApiFactory.TestOidcIdpAlias, "invalid-code"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Callback_UnknownIdpAlias_Returns401()
    {
        var client = _factory.CreateClient();
        var code = TestOidcCode.Encode("someone");

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, "no-such-idp", code));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Callback_SamlOnlyIdp_Returns401()
    {
        var client = _factory.CreateClient();
        var code = TestOidcCode.Encode("someone");

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, IdentityApiFactory.TestIdpAlias, code));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record TokenPairDto(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, string[] Roles, string[] Scopes);
}
