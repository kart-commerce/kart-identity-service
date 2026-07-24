using FluentValidation;

namespace Kart.Identity.Application.Features.UpdateProfile;

/// <summary>
/// api-contract.yaml PATCH /auth/profile request schema: `minProperties: 1`
/// (at least one of email/displayName must be supplied), email format when present.
/// </summary>
public sealed class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x)
            .Must(x => x.Email is not null || x.DisplayName is not null)
            .WithMessage("At least one of email or displayName must be supplied.");

        RuleFor(x => x.Email)
            .EmailAddress()
            .When(x => x.Email is not null);

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .When(x => x.DisplayName is not null);
    }
}
