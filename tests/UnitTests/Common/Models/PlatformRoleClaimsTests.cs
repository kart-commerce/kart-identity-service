using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Enums;
using Xunit;

namespace Kart.Identity.UnitTests.Common.Models;

public class PlatformRoleClaimsTests
{
    [Theory]
    [InlineData(PlatformRole.Customer, "customer")]
    [InlineData(PlatformRole.SupportAgent, "support_agent")]
    [InlineData(PlatformRole.Admin, "admin")]
    [InlineData(PlatformRole.PartnerApi, "partner_api")]
    public void ToClaimValue_RoundTripsThroughFromClaimValue(PlatformRole role, string claimValue)
    {
        Assert.Equal(claimValue, PlatformRoleClaims.ToClaimValue(role));
        Assert.Equal(role, PlatformRoleClaims.FromClaimValue(claimValue));
    }

    [Fact]
    public void FromClaimValue_UnknownValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlatformRoleClaims.FromClaimValue("not-a-role"));
    }
}
