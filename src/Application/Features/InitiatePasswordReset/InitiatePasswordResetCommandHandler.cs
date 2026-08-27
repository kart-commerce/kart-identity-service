using System.Text.Json;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.InitiatePasswordReset;

/// <summary>
/// api-contract.yaml POST /auth/password/reset-initiate — always responds 202
/// regardless of whether the email matches an account, to avoid
/// account-enumeration via response-shape difference; the no-op-when-unknown
/// path below is what makes that true rather than just documented.
///
/// Now enqueues a real `PasswordResetRequested` outbox event carrying the raw
/// token so kart-notification-service can deliver it — this used to be a flagged,
/// un-fixed gap (event-contract.md defined no password-reset event, so the raw
/// token only ever existed in this handler's own stack). Closed as part of the
/// User Registration, Login &amp; Authentication Journey flow build.
/// </summary>
public sealed class InitiatePasswordResetCommandHandler(
    IIdentityDbContext dbContext,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider,
    IPublicWebLinkBuilder publicWebLinkBuilder,
    ILogger<InitiatePasswordResetCommandHandler> logger)
    : IRequestHandler<InitiatePasswordResetCommand>
{
    public async Task Handle(InitiatePasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = EmailAddress.From(request.Email.Trim());
        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return;
        }

        var now = dateTimeProvider.UtcNow;
        var rawResetToken = opaqueTokenGenerator.Generate();
        var tokenHash = tokenHasher.Hash(rawResetToken);
        var resetToken = PasswordResetToken.Issue(user.UserId, tokenHash, now);

        var passwordResetRequested = OutboxEvent.Create(
            user.UserId.Value,
            "PasswordResetRequested",
            JsonSerializer.Serialize(new
            {
                userId = user.UserId.Value,
                email = user.Email?.Value,
                resetLink = publicWebLinkBuilder.PasswordResetConfirmLink(rawResetToken),
                expiresAt = resetToken.ExpiresAt
            }),
            now,
            createdBy: "system:password-reset");

        dbContext.PasswordResetTokens.Add(resetToken);
        dbContext.OutboxEvents.Add(passwordResetRequested);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Stage {Stage}: password reset token issued and outbox event saved for user {UserId}", "PasswordResetTokenIssued", user.UserId);
    }
}
