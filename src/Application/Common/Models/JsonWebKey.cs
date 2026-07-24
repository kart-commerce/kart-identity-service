namespace Kart.Identity.Application.Common.Models;

/// <summary>
/// RFC 7517 JWK shape, restricted to the RSA public-key fields an RS256 JWKS
/// document needs (design-decisions.md, "JWT Signing Algorithm").
/// </summary>
public sealed record JsonWebKey(
    string Kty,
    string Use,
    string Alg,
    string Kid,
    string N,
    string E);
