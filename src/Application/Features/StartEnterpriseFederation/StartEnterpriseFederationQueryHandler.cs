using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using MediatR;

namespace Kart.Identity.Application.Features.StartEnterpriseFederation;

/// <summary>
/// api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/login — SP-initiated
/// redirect, for either federation protocol a configured idpAlias speaks
/// (IDN-16). Builds the AuthnRequest (SAML) or authorization-code redirect
/// (OIDC) and hands the browser a Location header; no outbound call from
/// Identity to the IdP happens here in either case (design-decisions.md's
/// per-IdP circuit breaker/bulkhead applies to the OIDC token-exchange call at
/// the callback endpoint, not this pure URL-construction step).
/// </summary>
public sealed class StartEnterpriseFederationQueryHandler(
    IEnterpriseIdpDirectory idpDirectory,
    ISamlAuthnRequestBuilder authnRequestBuilder,
    IOidcAuthorizationRequestBuilder oidcAuthorizationRequestBuilder,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<StartEnterpriseFederationQuery, string>
{
    public Task<string> Handle(StartEnterpriseFederationQuery request, CancellationToken cancellationToken)
    {
        var idp = idpDirectory.Find(request.IdpAlias) ?? throw new EnterpriseIdpNotConfiguredException(request.IdpAlias);

        var redirectUrl = idp.Protocol == EnterpriseIdpProtocol.Oidc
            ? oidcAuthorizationRequestBuilder.BuildRedirectUrl(idp.Oidc!, state: Guid.NewGuid().ToString("N"))
            : authnRequestBuilder.BuildRedirectUrl(idp, dateTimeProvider.UtcNow);

        return Task.FromResult(redirectUrl);
    }
}
