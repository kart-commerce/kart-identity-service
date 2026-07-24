using System.Text.Json;
using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace Kart.Identity.Application.Features.RegisterUser;

/// <summary>
/// api-contract.yaml POST /auth/register — creates a native Kart account, grants
/// the default `Customer` role, and mints tokens immediately (MFA is off by default
/// for Customer, requirement-spec.md §2), publishing `UserRegistered` and
/// `SessionCreated` via the transactional outbox (design-decisions.md).
/// </summary>
public sealed class RegisterUserCommandHandler(
    IIdentityDbContext dbContext,
    IPasswordHasher passwordHasher,
    IAccessTokenGenerator accessTokenGenerator,
    IOpaqueTokenGenerator opaqueTokenGenerator,
    ITokenHasher tokenHasher,
    IDateTimeProvider dateTimeProvider)
    : IRequestHandler<RegisterUserCommand, RegisterUserResponse>
{
    public async Task<RegisterUserResponse> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();

        // uq_users_email is the authoritative guard against a concurrent duplicate
        // registration — this check only avoids paying the hashing/entity-creation
        // cost for the common (non-racing) case; the DbUpdateException catch below
        // is what actually closes the race.
        var emailTaken = await dbContext.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (emailTaken)
        {
            throw new EmailAlreadyRegisteredException(email);
        }

        var now = dateTimeProvider.UtcNow;
        var passwordHash = passwordHasher.Hash(request.Password);
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? email : request.DisplayName;

        var user = User.RegisterNative(email, passwordHash, displayName, now);
        var roleGrant = UserRole.GrantSelfRegisteredCustomer(user.UserId, now);
        var session = Session.CreateNative(user.UserId, now);

        var rawRefreshToken = opaqueTokenGenerator.Generate();
        var refreshTokenHash = tokenHasher.Hash(rawRefreshToken);
        var createdBy = user.UserId.ToString();
        var refreshToken = RefreshToken.IssueInitial(session.SessionId, refreshTokenHash, now, session.AbsoluteExpiresAt, createdBy);

        var roles = new[] { PlatformRoleClaims.ToClaimValue(PlatformRole.Customer) };
        var accessToken = accessTokenGenerator.Generate(user.UserId.ToString(), roles, scopes: []);

        var userRegistered = OutboxEvent.Create(
            user.UserId,
            "UserRegistered",
            JsonSerializer.Serialize(new { userId = user.UserId, email = user.Email }),
            now,
            createdBy);
        var sessionCreated = OutboxEvent.Create(
            user.UserId,
            "SessionCreated",
            JsonSerializer.Serialize(new { userId = user.UserId, sessionId = session.SessionId }),
            now,
            createdBy);

        dbContext.Users.Add(user);
        dbContext.UserRoles.Add(roleGrant);
        dbContext.Sessions.Add(session);
        dbContext.RefreshTokens.Add(refreshToken);
        dbContext.OutboxEvents.Add(userRegistered);
        dbContext.OutboxEvents.Add(sessionCreated);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Only uq_users_email can plausibly fail here — every other row's key is
            // a freshly generated Guid.
            throw new EmailAlreadyRegisteredException(email);
        }

        return new RegisterUserResponse(
            AccessToken: accessToken.Token,
            RefreshToken: rawRefreshToken,
            TokenType: "Bearer",
            ExpiresIn: accessToken.ExpiresInSeconds,
            Roles: roles,
            Scopes: []);
    }
}
