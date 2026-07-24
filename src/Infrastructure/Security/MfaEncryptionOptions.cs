using System.ComponentModel.DataAnnotations;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// Bound from configuration section "Mfa:Encryption". requirement-spec.md §4's PII
/// invariant: TOTP secrets are encrypted at rest with AES-256. <see cref="KeyBase64"/>
/// is a secret, supplied via environment variable / K8s Secret in production
/// (never committed) — same treatment as Jwt:SigningKey:PrivateKeyPem. Validated
/// at startup (<c>ValidateOnStart</c>) so a missing/malformed key fails fast.
/// </summary>
public sealed class MfaEncryptionOptions
{
    public const string SectionName = "Mfa:Encryption";

    /// <summary>Base64-encoded 256-bit (32-byte) AES key.</summary>
    [Required]
    public string KeyBase64 { get; init; } = string.Empty;
}
