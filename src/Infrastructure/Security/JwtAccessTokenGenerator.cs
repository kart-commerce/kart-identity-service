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

    /// <summary>
    /// Every downstream service's own `AuthenticationExtensions`/`DependencyInjection`
    /// (kart-user-service, kart-admin-service, kart-category-service, kart-order-service,
    /// kart-inventory-service, kart-cart-service, kart-product-service, kart-offer-service,
    /// kart-payment-service, kart-wishlist-service, kart-api-gateway — 11 services, confirmed by
    /// grep) sets `ValidateIssuer = true` against this exact literal, expecting Identity's own
    /// tokens to carry it. This service's own self-validation deliberately sets
    /// `ValidateIssuer = false` (see DependencyInjection.cs), so nothing here ever caught that no
    /// token this class minted ever actually carried an `iss` claim — every one of those 11
    /// services would 401 a real Identity-issued bearer token on first use, never caught by their
    /// own tests (header-driven fakes, not real JWT validation). Found and fixed here, at the
    /// single token-minting choke point, while building the User Registration, Login &amp;
    /// Authentication Journey flow (its own "Profile Setup"/"Save Address" steps hit this
    /// directly against kart-user-service).
    /// </summary>
    private const string Issuer = "kart-identity-service";

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
            issuer: Issuer,
            claims: claims,
            notBefore: now,
            expires: now.AddSeconds(AccessTokenLifetimeSeconds),
            signingCredentials: _signingCredentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new AccessToken(accessToken, AccessTokenLifetimeSeconds);
    }

    public void Dispose() => _rsa.Dispose();
}
