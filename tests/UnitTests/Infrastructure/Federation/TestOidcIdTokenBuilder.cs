using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.IdentityModel.Tokens;

namespace Kart.Identity.UnitTests.Infrastructure.Federation;

/// <summary>
/// Test-only helper that mints a signed OIDC id_token using a fresh self-signed
/// certificate, mirroring <c>TestSamlResponseBuilder</c>'s approach for SAML —
/// lets tests exercise <c>OidcTokenExchangeClient</c>'s real signature/issuer/
/// audience/expiry validation path rather than mocking it away.
/// </summary>
public static class TestOidcIdTokenBuilder
{
    public static (string IdToken, X509Certificate2 Certificate) BuildSignedIdToken(
        string issuer,
        string audience,
        string subject,
        string? email,
        IEnumerable<string> groups,
        DateTimeOffset now,
        TimeSpan? validity = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=test-oidc-idp", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeralCert = request.CreateSelfSigned(now.AddDays(-1).UtcDateTime, now.AddDays(1).UtcDateTime);
        var certificate = new X509Certificate2(ephemeralCert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);

        var claims = new List<Claim> { new("sub", subject) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
        }

        claims.AddRange(groups.Select(g => new Claim("groups", g)));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateJwtSecurityToken(
            issuer: issuer,
            audience: audience,
            subject: new ClaimsIdentity(claims),
            notBefore: now.AddMinutes(-1).UtcDateTime,
            expires: now.Add(validity ?? TimeSpan.FromMinutes(5)).UtcDateTime,
            signingCredentials: new SigningCredentials(new X509SecurityKey(certificate), SecurityAlgorithms.RsaSha256));

        return (handler.WriteToken(token), certificate);
    }
}
