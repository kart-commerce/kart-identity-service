using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.StartSocialLogin;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.StartSocialLogin;

public class StartSocialLoginQueryHandlerTests
{
    [Fact]
    public async Task Handle_ConfiguredProvider_ReturnsBuiltRedirectUrl()
    {
        var provider = new OidcProviderDescriptor(
            "google", "https://accounts.google.com/o/oauth2/auth", "https://oauth2.googleapis.com/token",
            "client-id", "client-secret", "https://identity.example.com/social/callback", "https://accounts.google.com", "cert-pem");
        var socialIdpDirectory = Substitute.For<ISocialIdpDirectory>();
        socialIdpDirectory.Find("google").Returns(provider);

        var authorizationRequestBuilder = Substitute.For<IOidcAuthorizationRequestBuilder>();
        authorizationRequestBuilder.BuildRedirectUrl(provider, Arg.Any<string>()).Returns("https://accounts.google.com/o/oauth2/auth?response_type=code");

        var handler = new StartSocialLoginQueryHandler(socialIdpDirectory, authorizationRequestBuilder);

        var redirectUrl = await handler.Handle(new StartSocialLoginQuery("google"), CancellationToken.None);

        Assert.Equal("https://accounts.google.com/o/oauth2/auth?response_type=code", redirectUrl);
    }

    [Fact]
    public async Task Handle_UnknownProvider_ThrowsSocialIdpNotConfigured()
    {
        var socialIdpDirectory = Substitute.For<ISocialIdpDirectory>();
        socialIdpDirectory.Find("unknown-provider").Returns((OidcProviderDescriptor?)null);

        var handler = new StartSocialLoginQueryHandler(socialIdpDirectory, Substitute.For<IOidcAuthorizationRequestBuilder>());

        await Assert.ThrowsAsync<SocialIdpNotConfiguredException>(
            () => handler.Handle(new StartSocialLoginQuery("unknown-provider"), CancellationToken.None));
    }
}
