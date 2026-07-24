using System.ComponentModel.DataAnnotations;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Bound from configuration section "Jwt:SigningKey". Per PLATFORM_BLUEPRINT.md's
/// Configuration Management layers, <see cref="PrivateKeyPem"/> is supplied via
/// environment variable / K8s Secret (never committed, never a literal in
/// appsettings.json) — <see cref="Kid"/> is a non-secret tunable and may live in
/// appsettings.json. Validated at startup (<c>ValidateOnStart</c>) so a missing
/// secret fails fast rather than surfacing as a 500 on the first JWKS/login request.
/// </summary>
public sealed class JwtSigningKeyOptions
{
    public const string SectionName = "Jwt:SigningKey";

    /// <summary>Key ID embedded in minted JWTs' "kid" header and in the JWKS document.</summary>
    [Required]
    public string Kid { get; init; } = string.Empty;

    /// <summary>PEM-encoded RSA private key (PKCS#1 or PKCS#8).</summary>
    [Required]
    public string PrivateKeyPem { get; init; } = string.Empty;
}
