using Kart.Identity.Application.Features.Login;
using Xunit;

namespace Kart.Identity.UnitTests.Features.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new LoginCommand("user@example.com", "any-password", "127.0.0.1"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "SomePassword1")]
    [InlineData("not-an-email", "SomePassword1")]
    [InlineData("user@example.com", "")]
    public void Validate_InvalidCommand_Fails(string email, string password)
    {
        var result = _validator.Validate(new LoginCommand(email, password, "127.0.0.1"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ShortPassword_StillPasses()
    {
        // Unlike registration, login only verifies an existing password — no
        // minLength constraint (api-contract.yaml POST /auth/login schema).
        var result = _validator.Validate(new LoginCommand("user@example.com", "short", "127.0.0.1"));

        Assert.True(result.IsValid);
    }
}
