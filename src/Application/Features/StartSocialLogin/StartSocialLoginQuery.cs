using MediatR;

namespace Kart.Identity.Application.Features.StartSocialLogin;

/// <summary>api-contract.yaml GET /auth/sso/social/{provider}/login.</summary>
public sealed record StartSocialLoginQuery(string Provider) : IRequest<string>;
