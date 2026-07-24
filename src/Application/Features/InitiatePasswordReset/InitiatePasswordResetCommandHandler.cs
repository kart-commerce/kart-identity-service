using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.InitiatePasswordReset;

/// <summary>
/// api-contract.yaml POST /auth/password/reset-initiate — always responds 202
/// regardless of whether the email matches an account, to avoid
/// account-enumeration via response-shape difference; the no-op-when-unknown
/// path below is what makes that true rather than just documented.
///
/// No email-delivery mechanism is wired here: event-contract.md defines no
/// password-reset-related event and requirement-spec.md's Consumes/Produces
/// tables name none for this endpoint either. The raw token exists only in this
/// handler's own stack — actual out-of-band delivery to the user is a genuine
/// gap, flagged rather than invented a fix for, same shape as tickets.md's
/// already-flagged native-role-elevation gap.
/// </summary>
public sealed class InitiatePasswordResetCommandHandler(
    IIdentityDbContext dbContext,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<InitiatePasswordResetCommand>
{
    public async Task Handle(InitiatePasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var rawResetToken = opaqueTokenGenerator.Generate();
        var tokenHash = tokenHasher.Hash(rawResetToken);
        var resetToken = PasswordResetToken.Issue(user.UserId, tokenHash, now);

        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
