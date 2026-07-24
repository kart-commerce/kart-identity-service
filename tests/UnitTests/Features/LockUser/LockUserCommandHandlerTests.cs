using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.LockUser;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.LockUser;

public class LockUserCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string LockedBy = "admin-service-principal";

    [Fact]
    public async Task Handle_ExistingUser_LocksUserAndRevokesLiveSessions()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (user, session) = await SeedUserWithLiveSessionAsync(dbContext);
        var revocationStore = Substitute.For<ITokenRevocationStore>();
        var handler = CreateHandler(dbContext, revocationStore);

        await handler.Handle(new LockUserCommand(user.UserId.ToString(), LockedBy), CancellationToken.None);

        var lockedUser = await dbContext.Users.SingleAsync(u => u.UserId == user.UserId);
        Assert.NotNull(lockedUser.LockedAt);
        Assert.Equal(LockedBy, lockedUser.LockedBy);

        var revokedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.NotNull(revokedSession.RevokedAt);
        Assert.Equal(SessionRevocationReason.AdminLock, revokedSession.RevokedReason);

        await revocationStore.Received(1).RevokeAllForUserAsync(user.UserId, FixedNow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownUserId_ThrowsUserNotFound()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => handler.Handle(new LockUserCommand(Guid.NewGuid().ToString(), LockedBy), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MalformedUserId_ThrowsUserNotFound()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => handler.Handle(new LockUserCommand("not-a-guid", LockedBy), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyRevokedSession_DoesNotOverwriteRevocationReason()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (user, session) = await SeedUserWithLiveSessionAsync(dbContext);
        session.Revoke(SessionRevocationReason.Logout, FixedNow.AddMinutes(-5), user.UserId.ToString());
        await dbContext.SaveChangesAsync();
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await handler.Handle(new LockUserCommand(user.UserId.ToString(), LockedBy), CancellationToken.None);

        var unchangedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(SessionRevocationReason.Logout, unchangedSession.RevokedReason);
    }

    private static async Task<(User User, Session Session)> SeedUserWithLiveSessionAsync(IdentityDbContext dbContext)
    {
        var user = User.RegisterNative("user@example.com", "hash", "Test User", FixedNow);
        var session = Session.CreateNative(user.UserId, FixedNow);

        dbContext.Users.Add(user);
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync();

        return (user, session);
    }

    private static LockUserCommandHandler CreateHandler(IIdentityDbContext dbContext, ITokenRevocationStore revocationStore)
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new LockUserCommandHandler(dbContext, revocationStore, dateTimeProvider);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
