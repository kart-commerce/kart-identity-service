using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.IdentityModel.Tokens;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Test double standing in for a real OIDC provider's token endpoint — decodes
/// the fake authorization code (<see cref="TestOidcCode"/>) and mints a real,
/// signed id_token for it, so <c>OidcTokenExchangeClient</c>'s real signature/
/// issuer/audience/expiry validation path is exercised rather than mocked away.
/// A code of <c>"invalid-code"</c> simulates the IdP rejecting the exchange.
/// </summary>
public sealed record FakeOidcProviderRegistration(string TokenEndpoint, string Issuer, string Audience, X509Certificate2 SigningCertificate);

/// <summary>
/// One instance is shared across every configured test IdP/provider (enterprise
/// OIDC and social both route through the single "oidc-token-exchange" named
/// HttpClient) — dispatches on the request's token-endpoint URL to mint an
/// id_token with the right issuer/audience/signing key for that provider.
/// </summary>
public sealed class FakeOidcTokenEndpointHandler(params FakeOidcProviderRegistration[] registrations) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var registration = registrations.SingleOrDefault(r => r.TokenEndpoint == request.RequestUri!.ToString())
            ?? throw new InvalidOperationException($"No fake OIDC provider registered for token endpoint {request.RequestUri}");

        var form = await request.Content!.ReadAsStringAsync(cancellationToken);
        var parsed = HttpUtility.ParseQueryString(form);
        var code = parsed["code"] ?? string.Empty;

        if (code == "invalid-code")
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{}") };
        }

        var claims = TestOidcCode.Decode(code);
        var idToken = BuildIdToken(claims, registration);
        var body = JsonSerializer.Serialize(new { id_token = idToken, access_token = "unused-access-token" });
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private static string BuildIdToken(TestOidcCode.Claims claims, FakeOidcProviderRegistration registration)
    {
        var jwtClaims = new List<Claim> { new("sub", claims.Subject) };
        if (claims.Email is not null)
        {
            jwtClaims.Add(new Claim("email", claims.Email));
        }

        jwtClaims.AddRange(claims.Groups.Select(g => new Claim("groups", g)));

        var handler = new JwtSecurityTokenHandler();
        var now = DateTimeOffset.UtcNow;
        var token = handler.CreateJwtSecurityToken(
            issuer: registration.Issuer,
            audience: registration.Audience,
            subject: new ClaimsIdentity(jwtClaims),
            notBefore: now.AddMinutes(-1).UtcDateTime,
            expires: now.AddMinutes(5).UtcDateTime,
            signingCredentials: new SigningCredentials(new X509SecurityKey(registration.SigningCertificate), SecurityAlgorithms.RsaSha256));

        return handler.WriteToken(token);
    }
}
