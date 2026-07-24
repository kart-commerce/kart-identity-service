using Kart.Identity.Application.Features.InitiatePasswordReset;
using Xunit;

namespace Kart.Identity.UnitTests.Features.InitiatePasswordReset;

public class InitiatePasswordResetCommandValidatorTests
{
    private readonly InitiatePasswordResetCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidEmail_Passes()
    {
        var result = _validator.Validate(new InitiatePasswordResetCommand("user@example.com"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_InvalidEmail_Fails(string email)
    {
        var result = _validator.Validate(new InitiatePasswordResetCommand(email));

        Assert.False(result.IsValid);
    }
}
