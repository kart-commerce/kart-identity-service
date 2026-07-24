using FluentValidation;

namespace Kart.Identity.Application.Features.ConfirmMfaEnrollment;

/// <summary>api-contract.yaml POST /auth/mfa/enroll/confirm request schema: totpCode is exactly 6 digits.</summary>
public sealed class ConfirmMfaEnrollmentCommandValidator : AbstractValidator<ConfirmMfaEnrollmentCommand>
{
    public ConfirmMfaEnrollmentCommandValidator()
    {
        RuleFor(x => x.TotpCode)
            .NotEmpty()
            .Matches("^[0-9]{6}$");
    }
}
