using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.EnterpriseSamlAssertionConsumer;

/// <summary>
/// api-contract.yaml POST /auth/sso/enterprise/{idpAlias}/saml/acs — validates the
/// signed assertion, rejects replay (edge-cases.md), JIT-provisions the Kart
/// account on first federation for this external identity (edge-cases.md,
/// "Federated Login With No Matching Kart Account"), resolves roles fresh
/// against `idp_group_role_mappings` on every login (fail-closed, never
/// persisted — design-decisions.md, "Caching Strategy for Role/Group-Mapping
/// Resolution"), and mints a federated session (24h absolute cap, no sliding).
/// </summary>
public sealed class EnterpriseSamlAssertionConsumerCommandHandler(
    IIdentityDbContext dbContext,
    IEnterpriseIdpDirectory idpDirectory,
    ISamlAssertionValidator samlAssertionValidator,
    ISamlAssertionReplayStore replayStore,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<EnterpriseSamlAssertionConsumerCommand, EnterpriseSamlAssertionConsumerResponse>
{
    public async Task<EnterpriseSamlAssertionConsumerResponse> Handle(
        EnterpriseSamlAssertionConsumerCommand request, CancellationToken cancellationToken)
    {
        // api-contract.yaml names no 404 for this endpoint specifically (only the
        // login-redirect one does) — an unconfigured idpAlias here is treated the
        // same as any other invalid-assertion case.
        var idp = idpDirectory.Find(request.IdpAlias) ?? throw new InvalidSamlAssertionException("idpAlias not configured");

        var now = dateTimeProvider.UtcNow;
        var assertion = samlAssertionValidator.ValidateAndExtract(request.SamlResponseBase64, idp, now);

        var replayTtl = assertion.NotOnOrAfter - now;
        var consumed = await replayStore.TryConsumeAsync(assertion.AssertionId, replayTtl, cancellationToken);
        if (!consumed)
        {
            throw new SamlAssertionReplayException();
        }

        var federatedIdentity = await dbContext.FederatedIdentities.SingleOrDefaultAsync(
            f => f.IdpType == FederatedIdpType.Enterprise && f.IdpKey == request.IdpAlias && f.ExternalSubjectId == assertion.NameId,
            cancellationToken);

        User user;
        var isNewUser = federatedIdentity is null;
        if (federatedIdentity is null)
        {
            user = User.ProvisionFederated(email: null, displayName: assertion.NameId, AccountOrigin.Enterprise, now);
            federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Enterprise, request.IdpAlias, assertion.NameId, now);
            dbContext.Users.Add(user);
            dbContext.FederatedIdentities.Add(federatedIdentity);
        }
        else
        {
            user = await dbContext.Users.SingleAsync(u => u.UserId == federatedIdentity.UserId, cancellationToken);

            // Same "ability to authenticate" invariant LoginCommandHandler enforces
            // for native login (IDN-10) — an admin-lock must hold across every
            // authentication path, not just the native one, or it isn't a lock.
            if (user.LockedAt is not null)
            {
                throw new AccountLockedException();
            }
        }

        var mappedRoles = await dbContext.IdpGroupRoleMappings
            .Where(m => m.IdpAlias == request.IdpAlias && assertion.GroupClaims.Contains(m.ExternalGroupClaim))
            .Select(m => m.Role)
            .Distinct()
            .ToListAsync(cancellationToken);
        var roleClaims = mappedRoles.Select(PlatformRoleClaims.ToClaimValue).ToArray();

        var session = Session.CreateFederated(user.UserId, request.IdpAlias, now);
        var createdBy = user.UserId.ToString();

        var rawRefreshToken = opaqueTokenGenerator.Generate();
        var refreshTokenHash = tokenHasher.Hash(rawRefreshToken);
        var refreshToken = RefreshToken.IssueInitial(session.SessionId, refreshTokenHash, now, session.AbsoluteExpiresAt, createdBy);

        dbContext.Sessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken);

        if (isNewUser)
        {
            // User Service treats this as its aggregate-creation trigger
            // (event-contract.md) regardless of which account-creation path
            // produced the new Identity row — JIT-provisioning via federation is
            // one such path, not just POST /auth/register.
            dbContext.OutboxEvents.Add(OutboxEvent.Create(
                user.UserId, "UserRegistered", JsonSerializer.Serialize(new { userId = user.UserId, email = user.Email }), now, createdBy));
        }

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            user.UserId, "SessionCreated", JsonSerializer.Serialize(new { userId = user.UserId, sessionId = session.SessionId }), now, createdBy));

        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenGenerator.Generate(createdBy, roleClaims, scopes: []);

        return new EnterpriseSamlAssertionConsumerResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: roleClaims,
            Scopes: []);
    }
}
