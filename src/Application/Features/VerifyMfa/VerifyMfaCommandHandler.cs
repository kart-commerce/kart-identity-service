using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.VerifyMfa;

/// <summary>
/// api-contract.yaml POST /auth/mfa/verify — completes the server-side-only
/// challenge Login (IDN-3) created for Admin/Support Agent (edge-cases.md,
/// "Partial-Auth Window During MFA": no token exists for the intermediate
/// state, only this challengeId). Mints a session exactly like Login's
/// already-authenticated branch, once the submitted TOTP code verifies against
/// the challenge's owner's confirmed credential (IDN-5).
/// </summary>
public sealed class VerifyMfaCommandHandler(
    IIdentityDbContext dbContext,
    IMfaChallengeStore mfaChallengeStore,
    IMfaSecretCipher mfaSecretCipher,
    ITotpCodeValidator totpCodeValidator,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<VerifyMfaCommand, VerifyMfaResponse>
{
    public async Task<VerifyMfaResponse> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        var challenge = await mfaChallengeStore.GetAndConsumeAsync(request.ChallengeId, cancellationToken);
        if (challenge is null)
        {
            throw new InvalidMfaChallengeException();
        }

        var credential = await dbContext.MfaCredentials.FindAsync([challenge.UserId], cancellationToken);
        if (credential is null || credential.Status != MfaCredentialStatus.Active)
        {
            throw new InvalidMfaChallengeException();
        }

        var secret = mfaSecretCipher.Decrypt(credential.EncryptedSecret);
        if (!totpCodeValidator.IsCodeValid(secret, request.TotpCode))
        {
            throw new InvalidMfaChallengeException();
        }

        var now = dateTimeProvider.UtcNow;
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

        return new VerifyMfaResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: challenge.Roles.ToArray(),
            Scopes: []);
    }
}
