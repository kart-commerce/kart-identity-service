using System.Security.Cryptography;
using Kart.Identity.Application.Common.Interfaces;
using Microsoft.IdentityModel.Tokens;
using JsonWebKey = Kart.Identity.Application.Common.Models.JsonWebKey;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Resolves the RS256 public key(s) this same service already publishes via JWKS
/// (<see cref="IJwtKeyProvider"/>) to validate an incoming Bearer token's
/// signature — Identity is both issuer and, for its own protected endpoints (e.g.
/// POST /auth/mfa/enroll), validator of its own tokens (design-decisions.md,
/// "JWT Signing Algorithm").
/// </summary>
public sealed class JwksIssuerSigningKeyResolver(IJwtKeyProvider keyProvider)
{
    public IEnumerable<SecurityKey> Resolve(
        string token, SecurityToken securityToken, string kid, TokenValidationParameters validationParameters) =>
        keyProvider.GetPublicSigningKeys()
            .Where(key => key.Kid == kid)
            .Select(ToRsaSecurityKey);

    private static RsaSecurityKey ToRsaSecurityKey(JsonWebKey jwk)
    {
        var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Base64UrlDecode(jwk.N),
            Exponent = Base64UrlDecode(jwk.E)
        });
        return new RsaSecurityKey(rsa) { KeyId = jwk.Kid };
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - (padded.Length % 4)) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
