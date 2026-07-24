using Kart.Identity.Application.Features.UpdateProfile;
using Xunit;

namespace Kart.Identity.UnitTests.Features.UpdateProfile;

public class UpdateProfileCommandValidatorTests
{
    private readonly UpdateProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_OnlyEmailSupplied_Passes()
    {
        var result = _validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), "new@example.com", null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_OnlyDisplayNameSupplied_Passes()
    {
        var result = _validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), null, "New Name"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_BothFieldsSupplied_Passes()
    {
        var result = _validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), "new@example.com", "New Name"));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_NeitherFieldSupplied_Fails()
    {
        var result = _validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), null, null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_MalformedEmail_Fails()
    {
        var result = _validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), "not-an-email", null));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_EmptyDisplayName_Fails()
    {
        var result = _validator.Validate(new UpdateProfileCommand(Guid.NewGuid(), null, ""));

        Assert.False(result.IsValid);
    }
}
