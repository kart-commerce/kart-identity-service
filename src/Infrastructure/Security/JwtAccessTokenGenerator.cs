using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Mints RS256-signed access tokens (design-decisions.md, "JWT Signing Algorithm").
/// Loads its own RSA instance from the same configured PEM as
/// <see cref="RsaJwtKeyProvider"/> — the private key never leaves this class; only
/// the public half is ever exposed elsewhere (via that provider's JWKS document).
/// </summary>
public sealed class JwtAccessTokenGenerator : IAccessTokenGenerator, IDisposable
{
    /// <summary>requirement-spec.md §4: access-token validity window (~15 min).</summary>
    private const int AccessTokenLifetimeSeconds = 900;

    private readonly RSA _rsa;
    private readonly SigningCredentials _signingCredentials;

    public JwtAccessTokenGenerator(IOptions<JwtSigningKeyOptions> options)
    {
        var configured = options.Value;
        _rsa = RSA.Create();
        _rsa.ImportFromPem(configured.PrivateKeyPem);

        var key = new RsaSecurityKey(_rsa) { KeyId = configured.Kid };
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    public AccessToken Generate(string subject, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> scopes)
    {
        var now = DateTime.UtcNow;
        // api-contract.yaml POST /auth/logout needs to address one specific,
        // already-issued access token in the revocation list (not every token this
        // subject holds) — `jti` is the standard JWT claim for that (edge-cases.md,
        // "Stale Revocation Under Stateless JWT Validation" names the mechanism but
        // not this claim-level detail).
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(role => new Claim("roles", role)));
        claims.AddRange(scopes.Select(scope => new Claim("scopes", scope)));

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: now,
            expires: now.AddSeconds(AccessTokenLifetimeSeconds),
            signingCredentials: _signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(accessToken, AccessTokenLifetimeSeconds);
    }

    public void Dispose() => _rsa.Dispose();
}
