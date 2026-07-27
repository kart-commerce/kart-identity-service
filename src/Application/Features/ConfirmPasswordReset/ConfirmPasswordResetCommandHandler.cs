using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.ConfirmPasswordReset;

/// <summary>
/// api-contract.yaml POST /auth/password/reset-confirm — single-use reset token
/// consumption via the same DB-conditional-update pattern as refresh-token
/// rotation (database-design.md), then revokes every outstanding session for the
/// user (`idx_sessions_user_live`, `revoked_reason = 'password_reset'`) so a
/// pre-reset session cannot remain valid, plus the per-user revocation-list
/// marker (same shape as LockUser) so already-minted, still-unexpired access
/// tokens are rejected by the Gateway too.
/// </summary>
public sealed class ConfirmPasswordResetCommandHandler(
    IIdentityDbContext dbContext,
    ITokenHasher tokenHasher,
    IPasswordHasher passwordHasher,
    ITokenRevocationStore revocationStore,
    IDateTimeProvider dateTimeProvider,
    ILogger<ConfirmPasswordResetCommandHandler> logger)
    : IRequestHandler<ConfirmPasswordResetCommand>
{
    public async Task Handle(ConfirmPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var hash = tokenHasher.Hash(request.ResetToken);
        var resetToken = await dbContext.PasswordResetTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        if (resetToken is null || resetToken.ConsumedAt is not null || resetToken.ExpiresAt <= now)
        {
            throw new InvalidOrExpiredPasswordResetTokenException();
        }

        var user = await dbContext.Users.SingleAsync(u => u.UserId == resetToken.UserId, cancellationToken);
        user.SetPassword(passwordHasher.Hash(request.NewPassword), now);
        resetToken.Consume(now);

        var liveSessions = await dbContext.Sessions
            .Where(s => s.UserId == user.UserId && s.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var session in liveSessions)
        {
            session.Revoke(SessionRevocationReason.PasswordReset, now, user.UserId.ToString());
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A concurrent confirm beat us to consuming this same reset token
            // between our read and this write — same DB-conditional-update race
            // RotateRefreshTokenCommandHandler already handles, but api-contract.yaml
            // names no 409 for this endpoint, so the loser is treated the same as
            // any other invalid/expired/already-used token.
            throw new InvalidOrExpiredPasswordResetTokenException();
        }

        await revocationStore.RevokeAllForUserAsync(user.UserId, now, cancellationToken);

        logger.LogInformation(
            "Password reset completed for user {UserId}, {SessionCount} sessions revoked",
            user.UserId,
            liveSessions.Count);
    }
}
