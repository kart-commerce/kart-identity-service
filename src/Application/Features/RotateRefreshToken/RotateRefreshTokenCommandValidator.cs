using FluentValidation;

namespace Kart.Identity.Application.Features.RotateRefreshToken;

/// <summary>api-contract.yaml POST /auth/refresh request schema: refreshToken required.</summary>
public sealed class RotateRefreshTokenCommandValidator : AbstractValidator<RotateRefreshTokenCommand>
{
    public RotateRefreshTokenCommandValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
