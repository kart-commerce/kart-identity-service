using Kart.Identity.Application.Features.RegisterUser;
using Xunit;

namespace Kart.Identity.UnitTests.Features.RegisterUser;

public class RegisterUserCommandValidatorTests
{
    private readonly RegisterUserCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new RegisterUserCommand("user@example.com", "SuperSecret1", "Jane Doe"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "SuperSecret1")]
    [InlineData("not-an-email", "SuperSecret1")]
    [InlineData("user@example.com", "")]
    [InlineData("user@example.com", "short")]
    public void Validate_InvalidCommand_Fails(string email, string password)
    {
        var result = _validator.Validate(new RegisterUserCommand(email, password, null));

        Assert.False(result.IsValid);
    }
}
