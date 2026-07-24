using Kart.Identity.Infrastructure.Security;
using OtpNet;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class TotpProvisioningServiceTests
{
    private readonly TotpProvisioningService _service = new();

    [Fact]
    public void GenerateEnrollment_ProducesAValidBase32Secret()
    {
        var enrollment = _service.GenerateEnrollment("user@example.com");

        // Throws if not valid base32 — round-trips through Otp.NET's own decoder.
        var decoded = Base32Encoding.ToBytes(enrollment.Secret);
        Assert.Equal(20, decoded.Length);
    }

    [Fact]
    public void GenerateEnrollment_ProvisioningUriMatchesOtpauthKeyUriFormat()
    {
        var enrollment = _service.GenerateEnrollment("user@example.com");

        var uri = new Uri(enrollment.ProvisioningUri);
        Assert.Equal("otpauth", uri.Scheme);
        Assert.Equal("totp", uri.Host);
        var decodedLabel = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        Assert.Equal("Kart:user@example.com", decodedLabel);
        Assert.Contains($"secret={enrollment.Secret}", enrollment.ProvisioningUri);
        Assert.Contains("issuer=Kart", enrollment.ProvisioningUri);
        Assert.Contains("algorithm=SHA1", enrollment.ProvisioningUri);
        Assert.Contains("digits=6", enrollment.ProvisioningUri);
        Assert.Contains("period=30", enrollment.ProvisioningUri);
    }

    [Fact]
    public void GenerateEnrollment_CalledTwice_ProducesDifferentSecrets()
    {
        var first = _service.GenerateEnrollment("user@example.com");
        var second = _service.GenerateEnrollment("user@example.com");

        Assert.NotEqual(first.Secret, second.Secret);
    }
}
