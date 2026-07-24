using Kart.Identity.Application.Common.Interfaces;
using OtpNet;

namespace Kart.Identity.Infrastructure.Security;

/// <summary>
/// RFC 6238 TOTP code verification (Otp.NET), counterpart to
/// <see cref="TotpProvisioningService"/>'s generation side. Allows one step
/// (30s) of clock skew either side — an engineering default; neither
/// requirement-spec.md nor api-contract.yaml name a concrete tolerance.
/// </summary>
public sealed class TotpCodeValidator : ITotpCodeValidator
{
    private static readonly VerificationWindow ClockSkewWindow = new(previous: 1, future: 1);

    public bool IsCodeValid(string base32Secret, string code)
    {
        var totp = new Totp(Base32Encoding.ToBytes(base32Secret));
        return totp.VerifyTotp(code, out _, ClockSkewWindow);
    }
}
