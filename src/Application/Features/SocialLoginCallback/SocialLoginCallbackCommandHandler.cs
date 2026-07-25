using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Application.Features.SocialLoginCallback;

/// <summary>
/// api-contract.yaml GET /auth/sso/social/{provider}/callback — customer social
/// login, JIT-provisioning a Kart account on first login for this external
/// identity (requirement-spec.md §2, same edge-cases.md JIT-provisioning
/// decision as enterprise federation). Unlike enterprise federation
/// (IDN-15/IDN-16), the resolved role is always exactly `Customer` — the social
/// IdP's own claims are never consulted for role elevation (requirement-spec.md
/// §2, resolved Open Question #7) — so, unlike the enterprise handlers, this one
/// never reads `idp_group_role_mappings` and never re-derives roles from
/// `user_roles` for a returning user either; the claim is fixed by construction.
/// </summary>
public sealed class SocialLoginCallbackCommandHandler(
    IIdentityDbContext dbContext,
    ISocialIdpDirectory socialIdpDirectory,
    IOidcTokenExchangeClient oidcTokenExchangeClient,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider,
    ILogger<SocialLoginCallbackCommandHandler> logger)
    : IRequestHandler<SocialLoginCallbackCommand, SocialLoginCallbackResponse>
{
    private static readonly string[] CustomerOnlyRoleClaims = [PlatformRoleClaims.ToClaimValue(PlatformRole.Customer)];

    public async Task<SocialLoginCallbackResponse> Handle(
        SocialLoginCallbackCommand request, CancellationToken cancellationToken)
    {
        var provider = socialIdpDirectory.Find(request.Provider)
            ?? throw new InvalidOidcTokenException("provider not configured for social login");

        var now = dateTimeProvider.UtcNow;
        var identity = await oidcTokenExchangeClient.ExchangeCodeAsync(provider, request.Code, now, cancellationToken);

        var federatedIdentity = await dbContext.FederatedIdentities.SingleOrDefaultAsync(
            f => f.IdpType == FederatedIdpType.Social && f.IdpKey == request.Provider && f.ExternalSubjectId == identity.Subject,
            cancellationToken);

        User user;
        var isNewUser = federatedIdentity is null;
        if (federatedIdentity is null)
        {
            user = User.ProvisionFederated(identity.Email, displayName: identity.Email ?? identity.Subject, AccountOrigin.Social, now);
            federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Social, request.Provider, identity.Subject, now);
            var roleGrant = UserRole.Grant(user.UserId, PlatformRole.Customer, grantedBy: "social-jit", now);
            dbContext.Users.Add(user);
            dbContext.FederatedIdentities.Add(federatedIdentity);
            dbContext.UserRoles.Add(roleGrant);
        }
        else
        {
            user = await dbContext.Users.SingleAsync(u => u.UserId == federatedIdentity.UserId, cancellationToken);

            // Same "ability to authenticate" invariant enforced by every other
            // login path (native, enterprise SAML/OIDC) — an admin-lock must hold
            // across every authentication path, not just one.
            if (user.LockedAt is not null)
            {
                throw new AccountLockedException();
            }
        }

        var session = Session.CreateFederated(user.UserId, request.Provider, now);
        var createdBy = user.UserId.ToString();

        var rawRefreshToken = opaqueTokenGenerator.Generate();
        var refreshTokenHash = tokenHasher.Hash(rawRefreshToken);
        var refreshToken = RefreshToken.IssueInitial(session.SessionId, refreshTokenHash, now, session.AbsoluteExpiresAt, createdBy);

        dbContext.Sessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken);

        if (isNewUser)
        {
            dbContext.OutboxEvents.Add(OutboxEvent.Create(
                user.UserId, "UserRegistered", JsonSerializer.Serialize(new { userId = user.UserId, email = user.Email }), now, createdBy));
        }

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            user.UserId, "SessionCreated", JsonSerializer.Serialize(new { userId = user.UserId, sessionId = session.SessionId }), now, createdBy));

        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenGenerator.Generate(createdBy, CustomerOnlyRoleClaims, scopes: []);

        logger.LogInformation(
            "Social login completed for user {UserId} via provider {Provider}, session {SessionId} created (newUser={IsNewUser})",
            user.UserId,
            request.Provider,
            session.SessionId,
            isNewUser);

        return new SocialLoginCallbackResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: CustomerOnlyRoleClaims,
            Scopes: []);
    }
}
