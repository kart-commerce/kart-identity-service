using FluentValidation;

namespace Kart.Identity.Application.Features.VerifyMfa;

/// <summary>api-contract.yaml POST /auth/mfa/verify request schema: both fields required, totpCode is exactly 6 digits.</summary>
public sealed class VerifyMfaCommandValidator : AbstractValidator<VerifyMfaCommand>
{
    public VerifyMfaCommandValidator()
    {
        RuleFor(x => x.ChallengeId).NotEmpty();

        RuleFor(x => x.TotpCode)
            .NotEmpty()
            .Matches("^[0-9]{6}$");
    }
}
