using System.Security.Cryptography;
using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.VerifyMfa;

/// <summary>
/// api-contract.yaml POST /auth/mfa/verify — completes the server-side-only
/// challenge Login (IDN-3) created for Admin/Support Agent (edge-cases.md,
/// "Partial-Auth Window During MFA": no token exists for the intermediate
/// state, only this challengeId). Mints a session exactly like Login's
/// already-authenticated branch, once the submitted TOTP code verifies against
/// the challenge's owner's credential (IDN-5).
///
/// Login gates Admin/Support Agent on an MFA challenge unconditionally
/// (LoginCommandHandler), before any credential is confirmed. A still-Pending,
/// not-yet-expired credential is therefore also accepted here: a valid code
/// both confirms the enrollment (mirrors ConfirmMfaEnrollmentCommandHandler)
/// and completes the login in the same call, so a user is never left holding
/// a challenge they have no bearer token to confirm enrollment against.
/// </summary>
public sealed class VerifyMfaCommandHandler(
    IIdentityDbContext dbContext,
    IMfaChallengeStore mfaChallengeStore,
    IMfaSecretCipher mfaSecretCipher,
    ITotpCodeValidator totpCodeValidator,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider,
    ILogger<VerifyMfaCommandHandler> logger)
    : IRequestHandler<VerifyMfaCommand, VerifyMfaResponse>
{
    public async Task<VerifyMfaResponse> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var challenge = await mfaChallengeStore.GetAndConsumeAsync(request.ChallengeId, cancellationToken);
        if (challenge is null)
        {
            throw new InvalidMfaChallengeException();
        }

        var now = dateTimeProvider.UtcNow;

        var credential = await dbContext.MfaCredentials.FindAsync([challenge.UserId], cancellationToken);
        var isConfirmablePending = credential is not null
            && credential.Status == MfaCredentialStatus.Pending
            && credential.PendingExpiresAt > now;
        if (credential is null || (credential.Status != MfaCredentialStatus.Active && !isConfirmablePending))
        {
            throw new InvalidMfaChallengeException();
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
            // non-disclosing failure used for every other reason this challenge
            // can't be completed.
            logger.LogError(
                ex,
                "MFA secret for user {UserId} could not be decrypted with the current encryption key",
                challenge.UserId);
            throw new InvalidMfaChallengeException();
        }

        if (!totpCodeValidator.IsCodeValid(secret, request.TotpCode))
        {
            throw new InvalidMfaChallengeException();
        }

        if (isConfirmablePending)
        {
            credential.Confirm(now);
        }

        var session = Session.CreateNative(challenge.UserId, now);
        var createdBy = challenge.UserId.ToString();

        var rawRefreshToken = opaqueTokenGenerator.Generate();
        var refreshTokenHash = tokenHasher.Hash(rawRefreshToken);
        var refreshToken = RefreshToken.IssueInitial(session.SessionId, refreshTokenHash, now, session.AbsoluteExpiresAt, createdBy);

        var sessionCreated = OutboxEvent.Create(
            challenge.UserId,
            "SessionCreated",
            JsonSerializer.Serialize(new { userId = challenge.UserId, sessionId = session.SessionId }),
            now,
            createdBy);

        dbContext.Sessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.OutboxEvents.Add(sessionCreated);
        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenGenerator.Generate(createdBy, challenge.Roles, scopes: []);

        logger.LogInformation(
            "MFA verified for user {UserId}, session {SessionId} created",
            challenge.UserId,
            session.SessionId);

        return new VerifyMfaResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: challenge.Roles.ToArray(),
            Scopes: []);
    }
}
