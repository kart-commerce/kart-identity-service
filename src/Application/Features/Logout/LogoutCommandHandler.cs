using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.Logout;

/// <summary>
/// api-contract.yaml POST /auth/logout — adds the presented access token to the
/// Redis-backed revocation list (edge-cases.md, "Stale Revocation Under Stateless
/// JWT Validation") and, if a refresh token was also presented, revokes that
/// session (== its whole rotation family, per RotateRefreshTokenCommandHandler's
/// same reasoning) via `sessions.revoked_reason = 'logout'`.
/// </summary>
public sealed class LogoutCommandHandler(
    IIdentityDbContext dbContext,
    ITokenRevocationStore revocationStore,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<LogoutCommand>
{
    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.UtcNow;
        var ttl = request.AccessTokenExpiresAt - now;
        if (ttl > TimeSpan.Zero)
        {
            await revocationStore.RevokeTokenAsync(request.Jti, ttl, cancellationToken);
        }

        if (request.RefreshToken is null)
        {
            return;
        }

        var hash = tokenHasher.Hash(request.RefreshToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
        {
            return;
        }

        var session = await dbContext.Sessions.SingleAsync(s => s.SessionId == token.SessionId, cancellationToken);
        if (session.RevokedAt is not null)
        {
            return;
        }

        session.Revoke(SessionRevocationReason.Logout, now, request.UserId.ToString());
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
