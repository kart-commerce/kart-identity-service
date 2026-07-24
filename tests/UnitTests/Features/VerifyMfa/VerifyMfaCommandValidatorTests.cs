using Kart.Identity.Application.Features.VerifyMfa;
using Xunit;

namespace Kart.Identity.UnitTests.Features.VerifyMfa;

public class VerifyMfaCommandValidatorTests
{
    private readonly VerifyMfaCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new VerifyMfaCommand("challenge-id", "123456"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "123456")]
    [InlineData("challenge-id", "")]
    [InlineData("challenge-id", "12345")]
    [InlineData("challenge-id", "abcdef")]
    public void Validate_InvalidCommand_Fails(string challengeId, string totpCode)
    {
        var result = _validator.Validate(new VerifyMfaCommand(challengeId, totpCode));

        Assert.False(result.IsValid);
    }
}
