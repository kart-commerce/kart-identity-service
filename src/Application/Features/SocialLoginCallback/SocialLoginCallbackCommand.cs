using MediatR;

namespace Kart.Identity.Application.Features.SocialLoginCallback;

/// <summary>api-contract.yaml GET /auth/sso/social/{provider}/callback.</summary>
public sealed record SocialLoginCallbackCommand(string Provider, string Code, string State)
    : IRequest<SocialLoginCallbackResponse>;
