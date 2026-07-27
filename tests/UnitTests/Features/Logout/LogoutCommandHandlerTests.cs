using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.Logout;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.Logout;

public class LogoutCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string RawRefreshToken = "raw-refresh-token";

    [Fact]
    public async Task Handle_AccessTokenOnly_RevokesTokenWithRemainingTtl()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var revocationStore = Substitute.For<ITokenRevocationStore>();
        var handler = CreateHandler(dbContext, revocationStore);
        var expiresAt = FixedNow.AddMinutes(10);

        await handler.Handle(new LogoutCommand(Guid.NewGuid(), "the-jti", expiresAt, null), CancellationToken.None);

        await revocationStore.Received(1).RevokeTokenAsync("the-jti", TimeSpan.FromMinutes(10), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyExpiredAccessToken_DoesNotWriteRevocationEntry()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var revocationStore = Substitute.For<ITokenRevocationStore>();
        var handler = CreateHandler(dbContext, revocationStore);

        await handler.Handle(new LogoutCommand(Guid.NewGuid(), "the-jti", FixedNow.AddMinutes(-1), null), CancellationToken.None);

        await revocationStore.DidNotReceive().RevokeTokenAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithRefreshToken_RevokesSessionWithLogoutReason()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (user, session, _) = await SeedSessionWithLiveTokenAsync(dbContext);
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await handler.Handle(new LogoutCommand(user.UserId, "the-jti", FixedNow.AddMinutes(10), RawRefreshToken), CancellationToken.None);

        var revokedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.NotNull(revokedSession.RevokedAt);
        Assert.Equal(SessionRevocationReason.Logout, revokedSession.RevokedReason);
    }

    [Fact]
    public async Task Handle_UnknownRefreshToken_DoesNotThrow()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await handler.Handle(new LogoutCommand(Guid.NewGuid(), "the-jti", FixedNow.AddMinutes(10), "no-such-token"), CancellationToken.None);
    }

    [Fact]
    public async Task Handle_AlreadyRevokedSession_DoesNotOverwriteRevocationReason()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (user, session, _) = await SeedSessionWithLiveTokenAsync(dbContext);
        session.Revoke(SessionRevocationReason.AdminLock, FixedNow.AddMinutes(-5), "admin-service");
        await dbContext.SaveChangesAsync();
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await handler.Handle(new LogoutCommand(user.UserId, "the-jti", FixedNow.AddMinutes(10), RawRefreshToken), CancellationToken.None);

        var unchangedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(SessionRevocationReason.AdminLock, unchangedSession.RevokedReason);
    }

    private static async Task<(User User, Session Session, RefreshToken Token)> SeedSessionWithLiveTokenAsync(IdentityDbContext dbContext)
    {
        var user = User.RegisterNative("user@example.com", "hash", "Test User", FixedNow);
        var session = Session.CreateNative(user.UserId, FixedNow);
        var token = RefreshToken.IssueInitial(session.SessionId, HashOf(RawRefreshToken), FixedNow, session.AbsoluteExpiresAt, user.UserId.ToString());

        dbContext.Users.Add(user);
        dbContext.Sessions.Add(session);
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        return (user, session, token);
    }

    private static string HashOf(string rawToken) => $"hash-of-{rawToken}";

    private static LogoutCommandHandler CreateHandler(IIdentityDbContext dbContext, ITokenRevocationStore revocationStore)
    {
        var tokenHasher = Substitute.For<ITokenHasher>();
        tokenHasher.Hash(Arg.Any<string>()).Returns(callInfo => HashOf(callInfo.Arg<string>()));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new LogoutCommandHandler(dbContext, revocationStore, tokenHasher, dateTimeProvider, NullLogger<LogoutCommandHandler>.Instance);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
