using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.EnrollMfa;

/// <summary>
/// api-contract.yaml POST /auth/mfa/enroll — issues a new TOTP secret (encrypted
/// at rest, requirement-spec.md §4) pending confirmation via
/// POST /auth/mfa/enroll/confirm (a later ticket). Re-enrolling replaces any
/// existing pending-or-active credential (database-design.md).
/// </summary>
public sealed class EnrollMfaCommandHandler(
    IIdentityDbContext dbContext,
    ITotpProvisioningService totpProvisioningService,
    IMfaSecretCipher mfaSecretCipher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<EnrollMfaCommand, EnrollMfaResponse>
{
    /// <summary>An unconfirmed enrollment attempt expires and must be restarted — an explicit engineering default, since neither requirement-spec.md nor api-contract.yaml name a concrete window.</summary>
    private static readonly TimeSpan PendingEnrollmentWindow = TimeSpan.FromMinutes(10);

    public async Task<EnrollMfaResponse> Handle(EnrollMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleAsync(u => u.UserId == request.UserId, cancellationToken);

        var enrollment = totpProvisioningService.GenerateEnrollment(accountLabel: user.Email ?? user.UserId.ToString());
        var encryptedSecret = mfaSecretCipher.Encrypt(enrollment.Secret);
        var now = dateTimeProvider.UtcNow;

        var credential = await dbContext.MfaCredentials.FindAsync([request.UserId], cancellationToken);
        if (credential is null)
        {
            credential = MfaCredential.BeginEnrollment(request.UserId, encryptedSecret, now, PendingEnrollmentWindow);
            dbContext.MfaCredentials.Add(credential);
        }
        else
        {
            credential.RestartEnrollment(encryptedSecret, now, PendingEnrollmentWindow);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new EnrollMfaResponse(enrollment.ProvisioningUri, credential.PendingExpiresAt!.Value);
    }
}
