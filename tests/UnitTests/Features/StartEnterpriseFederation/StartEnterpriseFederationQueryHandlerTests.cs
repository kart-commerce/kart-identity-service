using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.StartEnterpriseFederation;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.StartEnterpriseFederation;

public class StartEnterpriseFederationQueryHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ConfiguredIdp_ReturnsBuiltRedirectUrl()
    {
        var idp = new EnterpriseIdpDescriptor("okta-acme", "https://idp.example.com/sso", "sp-id", "https://identity.example.com/acs", "cert-pem");
        var idpDirectory = Substitute.For<IEnterpriseIdpDirectory>();
        idpDirectory.Find("okta-acme").Returns(idp);

        var authnRequestBuilder = Substitute.For<ISamlAuthnRequestBuilder>();
        authnRequestBuilder.BuildRedirectUrl(idp, FixedNow).Returns("https://idp.example.com/sso?SAMLRequest=abc");

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        var handler = new StartEnterpriseFederationQueryHandler(idpDirectory, authnRequestBuilder, dateTimeProvider);

        var redirectUrl = await handler.Handle(new StartEnterpriseFederationQuery("okta-acme"), CancellationToken.None);

        Assert.Equal("https://idp.example.com/sso?SAMLRequest=abc", redirectUrl);
    }

    [Fact]
    public async Task Handle_UnknownIdpAlias_ThrowsEnterpriseIdpNotConfigured()
    {
        var idpDirectory = Substitute.For<IEnterpriseIdpDirectory>();
        idpDirectory.Find("unknown-idp").Returns((EnterpriseIdpDescriptor?)null);

        var handler = new StartEnterpriseFederationQueryHandler(
            idpDirectory, Substitute.For<ISamlAuthnRequestBuilder>(), Substitute.For<IDateTimeProvider>());

        await Assert.ThrowsAsync<EnterpriseIdpNotConfiguredException>(
            () => handler.Handle(new StartEnterpriseFederationQuery("unknown-idp"), CancellationToken.None));
    }
}
