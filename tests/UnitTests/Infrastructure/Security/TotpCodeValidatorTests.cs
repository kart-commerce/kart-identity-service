using Kart.Identity.Infrastructure.Security;
using OtpNet;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class TotpCodeValidatorTests
{
    private readonly TotpCodeValidator _validator = new();

    [Fact]
    public void IsCodeValid_CurrentCode_ReturnsTrue()
    {
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);
        var currentCode = new Totp(secretBytes).ComputeTotp();

        Assert.True(_validator.IsCodeValid(base32Secret, currentCode));
    }

    [Fact]
    public void IsCodeValid_WrongCode_ReturnsFalse()
    {
        var secretBytes = KeyGeneration.GenerateRandomKey(20);
        var base32Secret = Base32Encoding.ToString(secretBytes);
        var currentCode = new Totp(secretBytes).ComputeTotp();
        var wrongCode = currentCode == "000000" ? "111111" : "000000";

        Assert.False(_validator.IsCodeValid(base32Secret, wrongCode));
    }

    [Fact]
    public void IsCodeValid_CodeForADifferentSecret_ReturnsFalse()
    {
        var base32Secret = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
        var codeFromAnotherSecret = new Totp(KeyGeneration.GenerateRandomKey(20)).ComputeTotp();

        Assert.False(_validator.IsCodeValid(base32Secret, codeFromAnotherSecret));
    }
}
