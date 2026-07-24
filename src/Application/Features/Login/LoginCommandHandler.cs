using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.Login;

/// <summary>
/// api-contract.yaml POST /auth/login. Session identifier is always freshly
/// generated on success (edge-cases.md, "Session Fixation via Pre-Auth Session
/// Reuse") — every successful login creates a brand-new `Session`/`RefreshToken`
/// pair, exactly like registration, so there is no pre-auth identifier to carry
/// over in the first place.
/// </summary>
public sealed class LoginCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider,
    ILoginAttemptThrottle loginAttemptThrottle,
    IMfaChallengeStore mfaChallengeStore)
    : IRequestHandler<LoginCommand, LoginResult>
{
    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        if (await loginAttemptThrottle.IsBlockedAsync(email, request.IpAddress, cancellationToken))
        {
            throw new LoginRateLimitExceededException();
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // IPasswordHasher.Verify pays an equivalent-cost dummy check when
        // user?.PasswordHash is null (unknown account, or a federated account
        // with no native password) so a wrong-password and a no-such-account
        // response aren't distinguishable by timing.
        if (!passwordHasher.Verify(request.Password, user?.PasswordHash))
        {
            await loginAttemptThrottle.RecordFailureAsync(email, request.IpAddress, cancellationToken);
            throw new InvalidCredentialsException();
        }

        // passwordHasher.Verify only returns true when user is non-null with a
        // non-null PasswordHash — unreachable otherwise, so this is safe.
        var authenticatedUser = user!;

        if (authenticatedUser.LockedAt is not null)
        {
            throw new AccountLockedException();
        }

        await loginAttemptThrottle.ResetAsync(email, request.IpAddress, cancellationToken);

        var roles = await dbContext.UserRoles
            .Where(r => r.UserId == authenticatedUser.UserId && r.RevokedAt == null)
            .Select(r => r.Role)
            .ToListAsync(cancellationToken);
        var roleClaims = roles.Select(PlatformRoleClaims.ToClaimValue).ToArray();

        // requirement-spec.md §2: MFA is mandatory at every login for Admin/Support
        // Agent. Customer's separate self-elected-MFA check is wired in once
        // IDN-4/IDN-5's `mfa_credentials` table exists — not invented here.
        var mfaRequired = roles.Contains(PlatformRole.Admin) || roles.Contains(PlatformRole.SupportAgent);
        if (mfaRequired)
        {
            var challenge = await mfaChallengeStore.CreateAsync(authenticatedUser.UserId, roleClaims, cancellationToken);
            return new MfaChallengeLoginResult(challenge.ChallengeId, challenge.ExpiresInSeconds);
        }

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

        var accessToken = accessTokenGenerator.Generate(authenticatedUser.UserId, roleClaims, scopes: []);

        return new AuthenticatedLoginResult(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: roleClaims,
            Scopes: []);
    }
}
