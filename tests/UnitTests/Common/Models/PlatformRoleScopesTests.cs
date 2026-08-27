using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Enums;
using Xunit;

namespace Kart.Identity.UnitTests.Common.Models;

public class PlatformRoleScopesTests
{
    [Theory]
    [InlineData(PlatformRole.Admin)]
    [InlineData(PlatformRole.SupportAgent)]
    public void ResolveScopes_AdminOrSupportAgent_IncludesAiAssistantQuery(PlatformRole role)
    {
        var scopes = PlatformRoleScopes.ResolveScopes([role]);

        Assert.Equal(["ai-assistant.query"], scopes);
    }

    [Theory]
    [InlineData(PlatformRole.Customer)]
    [InlineData(PlatformRole.PartnerApi)]
    public void ResolveScopes_CustomerOrPartnerApi_ReturnsNoScopes(PlatformRole role)
    {
        var scopes = PlatformRoleScopes.ResolveScopes([role]);

        Assert.Empty(scopes);
    }

    [Fact]
    public void ResolveScopes_MultipleRoles_ReturnsDeduplicatedUnion()
    {
        var scopes = PlatformRoleScopes.ResolveScopes([PlatformRole.Admin, PlatformRole.SupportAgent, PlatformRole.Customer]);

        Assert.Equal(["ai-assistant.query"], scopes);
    }

    [Fact]
    public void ResolveScopes_NoRoles_ReturnsEmpty()
    {
        var scopes = PlatformRoleScopes.ResolveScopes([]);

        Assert.Empty(scopes);
    }
}
