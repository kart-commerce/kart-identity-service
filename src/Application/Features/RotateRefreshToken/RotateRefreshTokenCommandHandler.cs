using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.RotateRefreshToken;

/// <summary>
/// api-contract.yaml POST /auth/refresh — rotates a single-use refresh token
/// (Domain Invariant §4). See RefreshTokenConfiguration for how the
/// `ConsumedAt` EF concurrency token realizes design-decisions.md's "DB-level
/// conditional update"; see edge-cases.md's "Refresh Token Replay After
/// Rotation" for why an already-consumed token replay revokes the session
/// (== the whole rotation family, since session_id never changes across a
/// chain's rotations).
/// </summary>
public sealed class RotateRefreshTokenCommandHandler(
    IIdentityDbContext dbContext,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider,
    ILogger<RotateRefreshTokenCommandHandler> logger)
    : IRequestHandler<RotateRefreshTokenCommand, RotateRefreshTokenResponse>
{
    public async Task<RotateRefreshTokenResponse> Handle(RotateRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var incomingHash = tokenHasher.Hash(request.RefreshToken);
        var token = await dbContext.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == incomingHash, cancellationToken);
        if (token is null)
        {
            throw new RefreshTokenReuseDetectedException();
        }

        var session = await dbContext.Sessions.SingleAsync(s => s.SessionId == token.SessionId, cancellationToken);
        var now = dateTimeProvider.UtcNow;

        if (token.ConsumedAt is not null || session.RevokedAt is not null)
        {
            if (session.RevokedAt is null)
            {
                session.Revoke(SessionRevocationReason.ReuseDetected, now, "system:identity-reuse-detection");
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // A likely token-theft signal (edge-cases.md, "Refresh Token Replay After
            // Rotation") — worth its own Warning with the session id, since the
            // generic boundary log (GlobalExceptionHandler) doesn't carry one.
            logger.LogWarning("Refresh token reuse detected for session {SessionId}, session revoked", session.SessionId);

            throw new RefreshTokenReuseDetectedException();
        }

        if (token.ExpiresAt <= now)
        {
            throw new RefreshTokenReuseDetectedException();
        }

        var updatedBy = session.UserId.ToString();
        var rawRefreshToken = opaqueTokenGenerator.Generate();
        var newTokenHash = tokenHasher.Hash(rawRefreshToken);
        var newToken = RefreshToken.IssueRotated(session.SessionId, token.TokenId, newTokenHash, now, session.AbsoluteExpiresAt, updatedBy);

        token.Consume(now, newToken.TokenId, updatedBy);
        dbContext.RefreshTokens.Add(newToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // edge-cases.md, "Concurrent Refresh Race": a concurrent request beat
            // us to consuming `token` between our read and this write.
            throw new RefreshTokenRaceLostException();
        }

        // design-decisions.md, "Caching Strategy for Role/Group-Mapping
        // Resolution (No Cache)" — re-resolved at every mint, refresh included,
        // not carried over from the original login's claims.
        var roles = await dbContext.UserRoles
            .Where(r => r.UserId == session.UserId && r.RevokedAt == null)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);
        var roleClaims = roles.Select(PlatformRoleClaims.ToClaimValue).ToArray();
        var accessToken = accessTokenGenerator.Generate(updatedBy, roleClaims, scopes: []);

        logger.LogInformation("Refresh token rotated for session {SessionId}", session.SessionId);

        return new RotateRefreshTokenResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: roleClaims,
            Scopes: []);
    }
}
