using System.Security.Cryptography;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Microsoft.Extensions.Options;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Loads the configured RS256 key pair once at construction and serves its
/// public half as a JWKS (design-decisions.md, "JWT Signing Algorithm": the
/// private key is held exclusively by Identity and is never exposed here).
/// </summary>
public sealed class RsaJwtKeyProvider : IJwtKeyProvider, IDisposable
{
    private const string KeyType = "RSA";
    private const string PublicKeyUse = "sig";
    private const string Algorithm = "RS256";

    private readonly RSA _rsa;
    private readonly IReadOnlyList<JsonWebKey> _publicSigningKeys;

    public RsaJwtKeyProvider(IOptions<JwtSigningKeyOptions> options)
    {
        var configured = options.Value;
        _rsa = RSA.Create();
        _rsa.ImportFromPem(configured.PrivateKeyPem);

        var publicParameters = _rsa.ExportParameters(includePrivateParameters: false);
        _publicSigningKeys =
        [
            new JsonWebKey(
                Kty: KeyType,
                Use: PublicKeyUse,
                Alg: Algorithm,
                Kid: configured.Kid,
                N: Base64UrlEncode(publicParameters.Modulus!),
                E: Base64UrlEncode(publicParameters.Exponent!))
        ];
    }

    public IReadOnlyList<JsonWebKey> GetPublicSigningKeys() => _publicSigningKeys;

    public void Dispose() => _rsa.Dispose();

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
