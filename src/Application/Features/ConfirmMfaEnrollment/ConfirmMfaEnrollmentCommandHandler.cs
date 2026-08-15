using System.Security.Cryptography;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

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
    IDateTimeProvider dateTimeProvider,
    ILogger<ConfirmMfaEnrollmentCommandHandler> logger)
    : IRequestHandler<ConfirmMfaEnrollmentCommand>
{
    public async Task Handle(ConfirmMfaEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var credential = await dbContext.MfaCredentials.FindAsync([request.UserId], cancellationToken);
        var now = dateTimeProvider.UtcNow;

        if (credential is null || credential.Status != MfaCredentialStatus.Pending || credential.PendingExpiresAt <= now)
        {
            logger.LogWarning("Stage {Stage}: MFA enrollment confirm rejected for user {UserId}, no valid pending enrollment", "InvalidOrExpiredMfaCode", request.UserId);
            throw new InvalidOrExpiredMfaCodeException();
        }

        string secret;
        try
        {
            secret = mfaSecretCipher.Decrypt(credential.EncryptedSecret);
        }
        catch (CryptographicException ex)
        {
            // Stored ciphertext no longer decrypts under the currently configured
            // key — AesMfaSecretCipher has no key versioning, so this only happens
            // if the encryption key was rotated after this credential was enrolled
            // (an operational/config issue, not a bug in the caller's request).
            // Logged at Error so it's distinguishable from an ordinary wrong-code
            // attempt, but still surfaced to the client as the same generic,
            // non-disclosing failure used for every other reason confirmation
            // can fail.
            logger.LogError(
                ex,
                "Stage {Stage}: MFA secret for user {UserId} could not be decrypted with the current encryption key",
                "InvalidOrExpiredMfaCode",
                request.UserId);
            throw new InvalidOrExpiredMfaCodeException();
        }

        if (!totpCodeValidator.IsCodeValid(secret, request.TotpCode))
        {
            logger.LogWarning("Stage {Stage}: MFA enrollment confirm rejected for user {UserId}, invalid TOTP code", "InvalidOrExpiredMfaCode", request.UserId);
            throw new InvalidOrExpiredMfaCodeException();
        }

        credential.Confirm(now);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: MFA enrollment confirmed for user {UserId}", "MfaEnrollmentConfirmationStepCompleted", request.UserId);
    }
}
