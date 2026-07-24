using MediatR;

namespace Kart.Identity.Application.Features.StartEnterpriseFederation;

/// <summary>api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/login. Returns the redirect URL.</summary>
public sealed record StartEnterpriseFederationQuery(string IdpAlias) : IRequest<string>;
