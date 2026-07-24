using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Enums;
using MediatR;

namespace Kart.Identity.Application.Features.ConfirmMfaEnrollment;

/// <summary>
/// api-contract.yaml POST /auth/mfa/enroll/confirm — activates the pending TOTP
/// credential POST /auth/mfa/enroll (IDN-4) created, once its first code
/// verifies (database-design.md `mfa_credentials` `pending` -> `active`).
/// </summary>
public sealed class ConfirmMfaEnrollmentCommandHandler(
    IIdentityDbContext dbContext,
    IMfaSecretCipher mfaSecretCipher,
    ITotpCodeValidator totpCodeValidator,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<ConfirmMfaEnrollmentCommand>
{
    public async Task Handle(ConfirmMfaEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var credential = await dbContext.MfaCredentials.FindAsync([request.UserId], cancellationToken);
        var now = dateTimeProvider.UtcNow;

        if (credential is null || credential.Status != MfaCredentialStatus.Pending || credential.PendingExpiresAt <= now)
        {
            throw new InvalidOrExpiredMfaCodeException();
        }

        var secret = mfaSecretCipher.Decrypt(credential.EncryptedSecret);
        if (!totpCodeValidator.IsCodeValid(secret, request.TotpCode))
        {
            throw new InvalidOrExpiredMfaCodeException();
        }

        credential.Confirm(now);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
