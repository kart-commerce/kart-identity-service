using Kart.Identity.Application.Features.ConfirmPasswordReset;
using Xunit;

namespace Kart.Identity.UnitTests.Features.ConfirmPasswordReset;

public class ConfirmPasswordResetCommandValidatorTests
{
    private readonly ConfirmPasswordResetCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new ConfirmPasswordResetCommand("some-token", "LongEnough1"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "LongEnough1")]
    [InlineData("some-token", "")]
    [InlineData("some-token", "short1")]
    public void Validate_InvalidCommand_Fails(string resetToken, string newPassword)
    {
        var result = _validator.Validate(new ConfirmPasswordResetCommand(resetToken, newPassword));

        Assert.False(result.IsValid);
    }
}
