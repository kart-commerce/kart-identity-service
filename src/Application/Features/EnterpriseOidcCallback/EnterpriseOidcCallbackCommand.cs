using MediatR;

namespace Kart.Identity.Application.Features.EnterpriseOidcCallback;

/// <summary>api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/oidc/callback.</summary>
public sealed record EnterpriseOidcCallbackCommand(string IdpAlias, string Code, string State)
    : IRequest<EnterpriseOidcCallbackResponse>;
