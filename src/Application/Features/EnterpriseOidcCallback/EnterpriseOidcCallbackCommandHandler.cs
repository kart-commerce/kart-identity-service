using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Features.EnterpriseOidcCallback;

/// <summary>
/// api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/oidc/callback — the
/// OIDC-flavored sibling of <see cref="Kart.Identity.Application.Features.EnterpriseSamlAssertionConsumer.EnterpriseSamlAssertionConsumerCommandHandler"/>:
/// same JIT-provisioning, account-lock, and fail-closed IdP-group-to-role-mapping
/// rules (requirement-spec.md §2, resolved Q7), reached via an authorization-code
/// token exchange instead of an inline signed-assertion check. No consumed-code
/// replay cache is needed here the way SAML needs an assertion-ID cache
/// (edge-cases.md, "SAML Assertion Replay at the ACS Endpoint") — an OIDC
/// authorization code is already single-use at the IdP's own token endpoint by
/// protocol design; a second replayed submission fails the token exchange itself.
/// </summary>
public sealed class EnterpriseOidcCallbackCommandHandler(
    IIdentityDbContext dbContext,
    IEnterpriseIdpDirectory idpDirectory,
    IOidcTokenExchangeClient oidcTokenExchangeClient,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<EnterpriseOidcCallbackCommand, EnterpriseOidcCallbackResponse>
{
    public async Task<EnterpriseOidcCallbackResponse> Handle(
        EnterpriseOidcCallbackCommand request, CancellationToken cancellationToken)
    {
        var idp = idpDirectory.Find(request.IdpAlias);
        if (idp is null || idp.Protocol != EnterpriseIdpProtocol.Oidc || idp.Oidc is null)
        {
            throw new InvalidOidcTokenException("idpAlias not configured for OIDC federation");
        }

        var now = dateTimeProvider.UtcNow;
        var identity = await oidcTokenExchangeClient.ExchangeCodeAsync(idp.Oidc, request.Code, now, cancellationToken);

        var federatedIdentity = await dbContext.FederatedIdentities.SingleOrDefaultAsync(
            f => f.IdpType == FederatedIdpType.Enterprise && f.IdpKey == request.IdpAlias && f.ExternalSubjectId == identity.Subject,
            cancellationToken);

        User user;
        var isNewUser = federatedIdentity is null;
        if (federatedIdentity is null)
        {
            user = User.ProvisionFederated(identity.Email, displayName: identity.Email ?? identity.Subject, AccountOrigin.Enterprise, now);
            federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Enterprise, request.IdpAlias, identity.Subject, now);
            dbContext.Users.Add(user);
            dbContext.FederatedIdentities.Add(federatedIdentity);
        }
        else
        {
            user = await dbContext.Users.SingleAsync(u => u.UserId == federatedIdentity.UserId, cancellationToken);

            // Same "ability to authenticate" invariant enforced by native login
            // (IDN-10) and the SAML ACS handler (IDN-15/IDN-16) — an admin-lock
            // must hold across every authentication path, not just one.
            if (user.LockedAt is not null)
            {
                throw new AccountLockedException();
            }
        }

        var mappedRoles = await dbContext.IdpGroupRoleMappings
            .Where(m => m.IdpAlias == request.IdpAlias && identity.GroupClaims.Contains(m.ExternalGroupClaim))
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
            dbContext.OutboxEvents.Add(OutboxEvent.Create(
                user.UserId, "UserRegistered", JsonSerializer.Serialize(new { userId = user.UserId, email = user.Email }), now, createdBy));
        }

        dbContext.OutboxEvents.Add(OutboxEvent.Create(
            user.UserId, "SessionCreated", JsonSerializer.Serialize(new { userId = user.UserId, sessionId = session.SessionId }), now, createdBy));

        await dbContext.SaveChangesAsync(cancellationToken);

        var accessToken = accessTokenGenerator.Generate(createdBy, roleClaims, scopes: []);

        return new EnterpriseOidcCallbackResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: roleClaims,
            Scopes: []);
    }
}
