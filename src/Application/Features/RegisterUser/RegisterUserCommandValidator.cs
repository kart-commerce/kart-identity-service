using FluentValidation;

namespace Kart.Identity.Application.Features.RegisterUser;

/// <summary>api-contract.yaml POST /auth/register request schema: email (required, format), password (required, minLength 8).</summary>
public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8);
    }
}
