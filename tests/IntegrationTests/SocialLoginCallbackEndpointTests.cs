using System.Net;
using System.Net.Http.Json;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Exercises api-contract.yaml GET /v1/auth/sso/social/{provider}/callback end
/// to end over real HTTP, with <see cref="FakeOidcTokenEndpointHandler"/>
/// standing in for the social provider's real token endpoint.
/// </summary>
public class SocialLoginCallbackEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string CallbackPathTemplate = "/v1/auth/sso/social/{0}/callback?code={1}&state=opaque-state";
    private readonly IdentityApiFactory _factory;

    public SocialLoginCallbackEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Callback_FirstLoginForExternalIdentity_Returns200AndJitProvisionsCustomerAccount()
    {
        var client = _factory.CreateClient();
        var subject = $"alice-{Guid.NewGuid():N}";
        var code = TestOidcCode.Encode(subject, $"{subject}@example.com");

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, IdentityApiFactory.TestSocialProvider, code));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenPairDto>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.Equal(["customer"], body.Roles);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var federatedIdentity = await dbContext.FederatedIdentities.SingleAsync(f => f.ExternalSubjectId == subject);
        Assert.Equal(FederatedIdpType.Social, federatedIdentity.IdpType);
        var roleGrant = await dbContext.UserRoles.SingleAsync(r => r.UserId == federatedIdentity.UserId);
        Assert.Equal("social-jit", roleGrant.GrantedBy);
    }

    [Fact]
    public async Task Callback_TokenExchangeFails_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, IdentityApiFactory.TestSocialProvider, "invalid-code"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Callback_UnknownProvider_Returns401()
    {
        var client = _factory.CreateClient();
        var code = TestOidcCode.Encode("someone");

        var response = await client.GetAsync(string.Format(CallbackPathTemplate, "no-such-provider", code));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record TokenPairDto(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, string[] Roles, string[] Scopes);
}
