using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using MediatR;

namespace Kart.Identity.Application.Features.StartEnterpriseFederation;

/// <summary>
/// api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/login — SP-initiated
/// redirect. Builds the AuthnRequest and hands the browser a Location header;
/// no outbound call from Identity to the IdP happens here (design-decisions.md's
/// per-IdP circuit breaker/bulkhead applies to a future OIDC token-exchange
/// call, not this pure URL-construction step).
/// </summary>
public sealed class StartEnterpriseFederationQueryHandler(
    IEnterpriseIdpDirectory idpDirectory,
    ISamlAuthnRequestBuilder authnRequestBuilder,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<StartEnterpriseFederationQuery, string>
{
    public Task<string> Handle(StartEnterpriseFederationQuery request, CancellationToken cancellationToken)
    {
        var idp = idpDirectory.Find(request.IdpAlias) ?? throw new EnterpriseIdpNotConfiguredException(request.IdpAlias);
        var redirectUrl = authnRequestBuilder.BuildRedirectUrl(idp, dateTimeProvider.UtcNow);
        return Task.FromResult(redirectUrl);
    }
}
