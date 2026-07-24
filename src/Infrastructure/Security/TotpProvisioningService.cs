using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using OtpNet;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// RFC 6238 TOTP secret generation (Otp.NET) — a 160-bit (20-byte) secret, the
/// library's own recommended default size for HMAC-SHA1-based TOTP. The otpauth://
/// URI follows the de facto Google Authenticator key-URI format for QR-code
/// enrollment.
/// </summary>
public sealed class TotpProvisioningService : ITotpProvisioningService
{
    private const string Issuer = "Kart";
    private const int SecretSizeBytes = 20;

    public TotpEnrollment GenerateEnrollment(string accountLabel)
    {
        var secretBytes = KeyGeneration.GenerateRandomKey(SecretSizeBytes);
        var base32Secret = Base32Encoding.ToString(secretBytes);

        var label = Uri.EscapeDataString($"{Issuer}:{accountLabel}");
        var issuer = Uri.EscapeDataString(Issuer);
        var provisioningUri = $"otpauth://totp/{label}?secret={base32Secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

        return new TotpEnrollment(base32Secret, provisioningUri);
    }
}
