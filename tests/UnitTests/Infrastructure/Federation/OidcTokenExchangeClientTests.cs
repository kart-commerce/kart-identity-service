using System.Net;
using System.Text;
using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Infrastructure.Federation;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Federation;

public class OidcTokenExchangeClientTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string Issuer = "https://login.example.com";
    private const string ClientId = "client-id";
    private const string ProviderKey = "azure-ad";

    [Fact]
    public async Task ExchangeCodeAsync_SuccessfulExchange_ReturnsValidatedIdentity()
    {
        var (idToken, certificate) = TestOidcIdTokenBuilder.BuildSignedIdToken(
            Issuer, ClientId, "alice-subject", "alice@example.com", ["Engineering"], FixedNow);
        var provider = BuildProvider(certificate.ExportCertificatePem());

        var client = CreateClient(FakeTokenEndpointHandler(HttpStatusCode.OK, idToken));

        var result = await client.ExchangeCodeAsync(provider, "auth-code", FixedNow, CancellationToken.None);

        Assert.Equal("alice-subject", result.Subject);
        Assert.Equal("alice@example.com", result.Email);
        Assert.Contains("Engineering", result.GroupClaims);
    }

    [Fact]
    public async Task ExchangeCodeAsync_TokenEndpointReturnsError_ThrowsInvalidOidcToken()
    {
        var provider = BuildProvider("unused-cert-pem-for-this-test");
        var client = CreateClient(FakeTokenEndpointHandler(HttpStatusCode.BadRequest, "{}"));

        await Assert.ThrowsAsync<InvalidOidcTokenException>(
            () => client.ExchangeCodeAsync(provider, "auth-code", FixedNow, CancellationToken.None));
    }

    [Fact]
    public async Task ExchangeCodeAsync_IdTokenSignedByWrongKey_ThrowsInvalidOidcToken()
    {
        var (idToken, _) = TestOidcIdTokenBuilder.BuildSignedIdToken(Issuer, ClientId, "alice-subject", null, [], FixedNow);
        var (_, unrelatedCertificate) = TestOidcIdTokenBuilder.BuildSignedIdToken(Issuer, ClientId, "someone-else", null, [], FixedNow);
        var provider = BuildProvider(unrelatedCertificate.ExportCertificatePem());

        var client = CreateClient(FakeTokenEndpointHandler(HttpStatusCode.OK, idToken));

        await Assert.ThrowsAsync<InvalidOidcTokenException>(
            () => client.ExchangeCodeAsync(provider, "auth-code", FixedNow, CancellationToken.None));
    }

    [Fact]
    public async Task ExchangeCodeAsync_ExpiredIdToken_ThrowsInvalidOidcToken()
    {
        var (idToken, certificate) = TestOidcIdTokenBuilder.BuildSignedIdToken(
            Issuer, ClientId, "alice-subject", null, [], FixedNow.AddMinutes(-10), validity: TimeSpan.FromMinutes(5));
        var provider = BuildProvider(certificate.ExportCertificatePem());

        var client = CreateClient(FakeTokenEndpointHandler(HttpStatusCode.OK, idToken));

        await Assert.ThrowsAsync<InvalidOidcTokenException>(
            () => client.ExchangeCodeAsync(provider, "auth-code", FixedNow, CancellationToken.None));
    }

    [Fact]
    public async Task ExchangeCodeAsync_WrongAudience_ThrowsInvalidOidcToken()
    {
        var (idToken, certificate) = TestOidcIdTokenBuilder.BuildSignedIdToken(
            Issuer, "some-other-client", "alice-subject", null, [], FixedNow);
        var provider = BuildProvider(certificate.ExportCertificatePem());

        var client = CreateClient(FakeTokenEndpointHandler(HttpStatusCode.OK, idToken));

        await Assert.ThrowsAsync<InvalidOidcTokenException>(
            () => client.ExchangeCodeAsync(provider, "auth-code", FixedNow, CancellationToken.None));
    }

    private static OidcProviderDescriptor BuildProvider(string signingCertificatePem) => new(
        ProviderKey, "https://login.example.com/authorize", "https://login.example.com/token",
        ClientId, "client-secret", "https://identity.example.com/oidc/callback", Issuer, signingCertificatePem);

    private static OidcTokenExchangeClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>()).Returns(httpClient);
        return new OidcTokenExchangeClient(factory);
    }

    private static HttpMessageHandler FakeTokenEndpointHandler(HttpStatusCode statusCode, string idToken)
    {
        var payload = statusCode == HttpStatusCode.OK
            ? JsonSerializer.Serialize(new { id_token = idToken, access_token = "unused" })
            : "{}";

        return new StubHttpMessageHandler((_, _) => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        });
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request, cancellationToken));
    }
}
