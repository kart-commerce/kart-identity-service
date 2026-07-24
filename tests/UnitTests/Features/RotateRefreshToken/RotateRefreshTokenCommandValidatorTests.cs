using Kart.Identity.Application.Features.RotateRefreshToken;
using Xunit;

namespace Kart.Identity.UnitTests.Features.RotateRefreshToken;

public class RotateRefreshTokenCommandValidatorTests
{
    private readonly RotateRefreshTokenCommandValidator _validator = new();

    [Fact]
    public void Validate_NonEmptyToken_Passes()
    {
        var result = _validator.Validate(new RotateRefreshTokenCommand("some-refresh-token"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyToken_Fails()
    {
        var result = _validator.Validate(new RotateRefreshTokenCommand(""));

        Assert.False(result.IsValid);
    }
}
