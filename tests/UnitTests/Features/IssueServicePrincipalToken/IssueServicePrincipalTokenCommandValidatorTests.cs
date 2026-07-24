using Kart.Identity.Application.Features.IssueServicePrincipalToken;
using Xunit;

namespace Kart.Identity.UnitTests.Features.IssueServicePrincipalToken;

public class IssueServicePrincipalTokenCommandValidatorTests
{
    private readonly IssueServicePrincipalTokenCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_Passes()
    {
        var result = _validator.Validate(new IssueServicePrincipalTokenCommand("client_credentials", "client-id", "client-secret", null));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("authorization_code", "client-id", "client-secret")]
    [InlineData("client_credentials", "", "client-secret")]
    [InlineData("client_credentials", "client-id", "")]
    public void Validate_InvalidCommand_Fails(string grantType, string clientId, string clientSecret)
    {
        var result = _validator.Validate(new IssueServicePrincipalTokenCommand(grantType, clientId, clientSecret, null));

        Assert.False(result.IsValid);
    }
}
