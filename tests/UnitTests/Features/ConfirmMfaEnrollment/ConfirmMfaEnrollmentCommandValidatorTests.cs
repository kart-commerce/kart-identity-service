using Kart.Identity.Application.Features.ConfirmMfaEnrollment;
using Xunit;

namespace Kart.Identity.UnitTests.Features.ConfirmMfaEnrollment;

public class ConfirmMfaEnrollmentCommandValidatorTests
{
    private readonly ConfirmMfaEnrollmentCommandValidator _validator = new();

    [Fact]
    public void Validate_SixDigitCode_Passes()
    {
        var result = _validator.Validate(new ConfirmMfaEnrollmentCommand(Guid.NewGuid(), "123456"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void Validate_NotSixDigits_Fails(string totpCode)
    {
        var result = _validator.Validate(new ConfirmMfaEnrollmentCommand(Guid.NewGuid(), totpCode));

        Assert.False(result.IsValid);
    }
}
