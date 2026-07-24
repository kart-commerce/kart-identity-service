using MediatR;

namespace Kart.Identity.Application.Features.EnterpriseSamlAssertionConsumer;

/// <summary>api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs.</summary>
public sealed record EnterpriseSamlAssertionConsumerCommand(string IdpAlias, string SamlResponseBase64)
    : IRequest<EnterpriseSamlAssertionConsumerResponse>;
