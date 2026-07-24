using FluentValidation;

namespace Kart.Identity.Application.Features.Login;

/// <summary>api-contract.yaml POST /auth/login request schema: email + password required, no format constraint on password (unlike registration — this only verifies an existing one).</summary>
public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
