using FluentValidation;

namespace Kart.Identity.Application.Features.InitiatePasswordReset;

/// <summary>api-contract.yaml POST /auth/password/reset-initiate request schema: email required.</summary>
public sealed class InitiatePasswordResetCommandValidator : AbstractValidator<InitiatePasswordResetCommand>
{
    public InitiatePasswordResetCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();
    }
}
