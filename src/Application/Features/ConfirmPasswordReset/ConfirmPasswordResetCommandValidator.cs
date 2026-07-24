using FluentValidation;

namespace Kart.Identity.Application.Features.ConfirmPasswordReset;

/// <summary>api-contract.yaml POST /auth/password/reset-confirm request schema: resetToken + newPassword (minLength 8) required.</summary>
public sealed class ConfirmPasswordResetCommandValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    public ConfirmPasswordResetCommandValidator()
    {
        RuleFor(x => x.ResetToken).NotEmpty();

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8);
    }
}
