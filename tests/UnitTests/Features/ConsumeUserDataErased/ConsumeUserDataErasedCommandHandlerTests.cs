using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.ConsumeUserDataErased;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.ConsumeUserDataErased;

public class ConsumeUserDataErasedCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ErasesUserPiiDeletesMfaAndFederatedIdentitiesAndRevokesLiveSessions()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("alice@example.com", "hash", "Alice", FixedNow.AddDays(-30));
        var mfaCredential = MfaCredential.BeginEnrollment(user.UserId, [1, 2, 3], FixedNow.AddDays(-10), TimeSpan.FromMinutes(10));
        mfaCredential.Confirm(FixedNow.AddDays(-10));
        var federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Social, "google", "alice-subject", FixedNow.AddDays(-10));
        var session = Session.CreateNative(user.UserId, FixedNow.AddDays(-1));

        dbContext.Users.Add(user);
        dbContext.MfaCredentials.Add(mfaCredential);
        dbContext.FederatedIdentities.Add(federatedIdentity);
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync();

        var revocationStore = Substitute.For<ITokenRevocationStore>();
        var handler = CreateHandler(dbContext, revocationStore);

        await handler.Handle(new ConsumeUserDataErasedCommand(user.UserId, FixedNow), CancellationToken.None);

        var erasedUser = await dbContext.Users.SingleAsync();
        Assert.Null(erasedUser.Email);
        Assert.Equal("[erased]", erasedUser.DisplayName);
        Assert.Null(erasedUser.PasswordHash);
        Assert.Equal("system:user-service-erasure-consumer", erasedUser.UpdatedBy);

        Assert.Empty(await dbContext.MfaCredentials.ToListAsync());
        Assert.Empty(await dbContext.FederatedIdentities.ToListAsync());

        var revokedSession = await dbContext.Sessions.SingleAsync();
        Assert.NotNull(revokedSession.RevokedAt);
        Assert.Equal(SessionRevocationReason.Erasure, revokedSession.RevokedReason);

        await revocationStore.Received(1).RevokeAllForUserAsync(user.UserId, FixedNow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserWithNoMfaOrFederatedIdentity_DoesNotThrow()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("bob@example.com", "hash", "Bob", FixedNow.AddDays(-30));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await handler.Handle(new ConsumeUserDataErasedCommand(user.UserId, FixedNow), CancellationToken.None);

        var erasedUser = await dbContext.Users.SingleAsync();
        Assert.Null(erasedUser.Email);
    }

    [Fact]
    public async Task Handle_AlreadyErasedUser_RedeliveryIsIdempotentNoOp()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("carol@example.com", "hash", "Carol", FixedNow.AddDays(-30));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var revocationStore = Substitute.For<ITokenRevocationStore>();
        var handler = CreateHandler(dbContext, revocationStore);

        await handler.Handle(new ConsumeUserDataErasedCommand(user.UserId, FixedNow), CancellationToken.None);
        await handler.Handle(new ConsumeUserDataErasedCommand(user.UserId, FixedNow.AddMinutes(5)), CancellationToken.None);

        var erasedUser = await dbContext.Users.SingleAsync();
        Assert.Null(erasedUser.Email);
        Assert.Equal("[erased]", erasedUser.DisplayName);
        await revocationStore.Received(2).RevokeAllForUserAsync(user.UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownUserId_NoOpDoesNotThrow()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var revocationStore = Substitute.For<ITokenRevocationStore>();
        var handler = CreateHandler(dbContext, revocationStore);

        await handler.Handle(new ConsumeUserDataErasedCommand(Guid.NewGuid(), FixedNow), CancellationToken.None);

        await revocationStore.DidNotReceive().RevokeAllForUserAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyRevokedSession_LeftUntouched()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("dave@example.com", "hash", "Dave", FixedNow.AddDays(-30));
        var session = Session.CreateNative(user.UserId, FixedNow.AddDays(-1));
        session.Revoke(SessionRevocationReason.Logout, FixedNow.AddHours(-1), user.UserId.ToString());
        dbContext.Users.Add(user);
        dbContext.Sessions.Add(session);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());
        await handler.Handle(new ConsumeUserDataErasedCommand(user.UserId, FixedNow), CancellationToken.None);

        var persistedSession = await dbContext.Sessions.SingleAsync();
        Assert.Equal(SessionRevocationReason.Logout, persistedSession.RevokedReason);
    }

    private static ConsumeUserDataErasedCommandHandler CreateHandler(IIdentityDbContext dbContext, ITokenRevocationStore revocationStore)
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);
        return new ConsumeUserDataErasedCommandHandler(dbContext, revocationStore, dateTimeProvider, NullLogger<ConsumeUserDataErasedCommandHandler>.Instance);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
