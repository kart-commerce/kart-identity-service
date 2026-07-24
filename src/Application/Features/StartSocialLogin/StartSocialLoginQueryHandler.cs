using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using MediatR;

namespace Kart.Identity.Application.Features.StartSocialLogin;

/// <summary>
/// api-contract.yaml GET /auth/sso/social/{provider}/login — SP-initiated OIDC
/// authorization-code redirect for customer social login, the social-login
/// sibling of <see cref="Kart.Identity.Application.Features.StartEnterpriseFederation.StartEnterpriseFederationQueryHandler"/>'s
/// OIDC branch. No outbound call from Identity to the provider happens here
/// (design-decisions.md's per-provider circuit breaker/bulkhead applies to the
/// token-exchange call at the callback endpoint, not this pure URL-construction step).
/// </summary>
public sealed class StartSocialLoginQueryHandler(
    ISocialIdpDirectory socialIdpDirectory,
    IOidcAuthorizationRequestBuilder oidcAuthorizationRequestBuilder)
    : IRequestHandler<StartSocialLoginQuery, string>
{
    public Task<string> Handle(StartSocialLoginQuery request, CancellationToken cancellationToken)
    {
        var provider = socialIdpDirectory.Find(request.Provider) ?? throw new SocialIdpNotConfiguredException(request.Provider);
        var redirectUrl = oidcAuthorizationRequestBuilder.BuildRedirectUrl(provider, state: Guid.NewGuid().ToString("N"));
        return Task.FromResult(redirectUrl);
    }
}
