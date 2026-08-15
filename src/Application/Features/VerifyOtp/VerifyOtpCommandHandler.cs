using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.Login;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.VerifyOtp;

/// <summary>
/// api-contract.yaml POST /auth/otp/verify. Session/token minting on success is the
/// exact same tail as LoginCommandHandler (fresh Session/RefreshToken pair, same
/// mandatory-MFA-role gate for Admin/Support Agent) — deliberately duplicated rather
/// than extracted, matching this codebase's own duplicate-per-slice precedent (see
/// LoginCommandHandler's VerifyMfaCommandHandler sibling for the same shape).
/// </summary>
public sealed class VerifyOtpCommandHandler(
    IIdentityDbContext dbContext,
    IOtpCodeStore otpCodeStore,
    IOtpAttemptThrottle otpAttemptThrottle,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider,
    IMfaChallengeStore mfaChallengeStore,
    ILogger<VerifyOtpCommandHandler> logger)
    : IRequestHandler<VerifyOtpCommand, LoginResult>
{
    public async Task<LoginResult> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        if (await otpAttemptThrottle.IsBlockedAsync(email, request.IpAddress, cancellationToken))
        {
            logger.LogWarning("Stage {Stage}: OTP verify rejected for {Email}, rate limit exceeded", "OtpRateLimitExceeded", email);
            throw new OtpRateLimitExceededException();
        }

        var userId = await otpCodeStore.VerifyAndConsumeAsync(email, request.Code, cancellationToken);
        if (userId is null)
        {
            await otpAttemptThrottle.RecordAttemptAsync(email, request.IpAddress, cancellationToken);
            logger.LogWarning("Stage {Stage}: OTP verify rejected for {Email}, invalid or expired code", "InvalidOrExpiredOtpCode", email);
            throw new InvalidOrExpiredOtpCodeException();
        }

        var authenticatedUser = await dbContext.Users.SingleAsync(u => u.UserId == userId.Value, cancellationToken);
        if (authenticatedUser.LockedAt is not null)
        {
            logger.LogWarning("Stage {Stage}: OTP verify rejected for user {UserId}, account locked", "AccountLocked", authenticatedUser.UserId);
            throw new AccountLockedException();
        }

        await otpAttemptThrottle.ResetAsync(email, request.IpAddress, cancellationToken);
        logger.LogInformation("Stage {Stage}: OTP verified for user {UserId}", "OtpVerified", authenticatedUser.UserId);

        var roles = await dbContext.UserRoles
            .Where(r => r.UserId == authenticatedUser.UserId && r.RevokedAt == null)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);
        var roleClaims = roles.Select(PlatformRoleClaims.ToClaimValue).ToArray();

        var mfaRequired = roles.Contains(PlatformRole.Admin) || roles.Contains(PlatformRole.SupportAgent);
        if (mfaRequired)
        {
            var challenge = await mfaChallengeStore.CreateAsync(authenticatedUser.UserId, roleClaims, cancellationToken);
            logger.LogInformation("Stage {Stage}: MFA challenge issued for user {UserId} after OTP verification", "MfaChallengeIssued", authenticatedUser.UserId);
            return new MfaChallengeLoginResult(challenge.ChallengeId, challenge.ExpiresInSeconds);
        }

        logger.LogInformation("Stage {Stage}: MFA not required, issuing tokens directly for user {UserId}", "MfaNotRequiredTokensIssued", authenticatedUser.UserId);

        var now = dateTimeProvider.UtcNow;
        var session = Session.CreateNative(authenticatedUser.UserId, now);
        var createdBy = authenticatedUser.UserId.ToString();

        var rawRefreshToken = opaqueTokenGenerator.Generate();
        var refreshTokenHash = tokenHasher.Hash(rawRefreshToken);
        var refreshToken = RefreshToken.IssueInitial(session.SessionId, refreshTokenHash, now, session.AbsoluteExpiresAt, createdBy);

        var sessionCreated = OutboxEvent.Create(
            authenticatedUser.UserId,
            "SessionCreated",
            JsonSerializer.Serialize(new { userId = authenticatedUser.UserId, sessionId = session.SessionId }),
            now,
            createdBy);

        dbContext.Sessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.OutboxEvents.Add(sessionCreated);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Stage {Stage}: session {SessionId} persisted for user {UserId}, outbox event {SessionCreatedEventId} (SessionCreated) enqueued",
            "SessionPersistedOutboxEventEnqueued",
            session.SessionId,
            authenticatedUser.UserId,
            sessionCreated.EventId);

        var accessToken = accessTokenGenerator.Generate(authenticatedUser.UserId.ToString(), roleClaims, scopes: []);

        logger.LogInformation(
            "Stage {Stage}: user {UserId} logged in via OTP, session {SessionId} created",
            "OtpLoginProcessCompletedSuccessfully",
            authenticatedUser.UserId,
            session.SessionId);

        return new AuthenticatedLoginResult(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: roleClaims,
            Scopes: []);
    }
}
