using System.Net;
using System.Net.Http.Json;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Exercises api-contract.yaml POST /v1/auth/sso/enterprise/{idpAlias}/saml/acs
/// end to end over real HTTP, signing fake SAML responses with the same
/// certificate the test host's `test-idp` is configured to trust.
/// </summary>
public class EnterpriseSamlAssertionConsumerEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string AcsPathTemplate = "/v1/auth/sso/enterprise/{0}/saml/acs";
    private readonly IdentityApiFactory _factory;

    public EnterpriseSamlAssertionConsumerEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Acs_FirstLoginForExternalIdentity_Returns200AndJitProvisionsAccount()
    {
        var client = _factory.CreateClient();
        var nameId = $"alice-{Guid.NewGuid():N}@example.com";
        var samlResponse = TestSamlResponseBuilder.BuildSignedResponse(
            _factory.TestIdpCertificate, IdentityApiFactory.TestIdpSpEntityId, nameId, [], DateTimeOffset.UtcNow);

        var response = await PostAcsAsync(client, samlResponse);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenPairDto>();
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.False(string.IsNullOrEmpty(body.RefreshToken));
        Assert.Empty(body.Roles);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var federatedIdentity = await dbContext.FederatedIdentities.SingleAsync(f => f.ExternalSubjectId == nameId);
        Assert.Equal(FederatedIdpType.Enterprise, federatedIdentity.IdpType);
        var session = await dbContext.Sessions.SingleAsync(s => s.UserId == federatedIdentity.UserId);
        Assert.True(session.IsFederated);
    }

    [Fact]
    public async Task Acs_MappedGroup_ReturnsMappedRole()
    {
        var nameId = $"bob-{Guid.NewGuid():N}@example.com";
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            dbContext.IdpGroupRoleMappings.Add(
                IdpGroupRoleMapping.Create(IdentityApiFactory.TestIdpAlias, "Engineering", PlatformRole.SupportAgent, DateTimeOffset.UtcNow, "operator"));
            await dbContext.SaveChangesAsync();
        }

        var client = _factory.CreateClient();
        var samlResponse = TestSamlResponseBuilder.BuildSignedResponse(
            _factory.TestIdpCertificate, IdentityApiFactory.TestIdpSpEntityId, nameId, ["Engineering"], DateTimeOffset.UtcNow);

        var response = await PostAcsAsync(client, samlResponse);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TokenPairDto>();
        Assert.Equal(["support_agent"], body!.Roles);
    }

    [Fact]
    public async Task Acs_ReplayedAssertion_Returns409()
    {
        var client = _factory.CreateClient();
        var nameId = $"replay-{Guid.NewGuid():N}@example.com";
        var samlResponse = TestSamlResponseBuilder.BuildSignedResponse(
            _factory.TestIdpCertificate, IdentityApiFactory.TestIdpSpEntityId, nameId, [], DateTimeOffset.UtcNow);

        var first = await PostAcsAsync(client, samlResponse);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await PostAcsAsync(client, samlResponse);
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task Acs_TamperedSignature_Returns401()
    {
        var client = _factory.CreateClient();
        var samlResponse = TestSamlResponseBuilder.BuildSignedResponse(
            _factory.TestIdpCertificate, IdentityApiFactory.TestIdpSpEntityId, "mallory@example.com", [], DateTimeOffset.UtcNow);
        var tampered = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(samlResponse)).Replace("mallory", "not-mallory");
        var tamperedResponse = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(tampered));

        var response = await PostAcsAsync(client, tamperedResponse);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAcsAsync(HttpClient client, string samlResponseBase64)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string> { ["SAMLResponse"] = samlResponseBase64 });
        return await client.PostAsync(string.Format(AcsPathTemplate, IdentityApiFactory.TestIdpAlias), content);
    }

    private sealed record TokenPairDto(string AccessToken, string RefreshToken, string TokenType, int ExpiresIn, string[] Roles, string[] Scopes);
}
