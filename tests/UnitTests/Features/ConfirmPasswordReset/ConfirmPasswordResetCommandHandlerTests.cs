using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.ConfirmPasswordReset;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.ConfirmPasswordReset;

public class ConfirmPasswordResetCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string RawResetToken = "raw-reset-token";
    private const string NewPassword = "BrandNewSecret1";
    private const string NewPasswordHash = "hash-of-new-password";

    [Fact]
    public async Task Handle_ValidToken_SetsNewPasswordConsumesTokenAndRevokesLiveSessions()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (user, session, _) = await SeedUserWithResetTokenAndLiveSessionAsync(dbContext);
        var revocationStore = Substitute.For<ITokenRevocationStore>();
        var handler = CreateHandler(dbContext, revocationStore);

        await handler.Handle(new ConfirmPasswordResetCommand(RawResetToken, NewPassword), CancellationToken.None);

        var updatedUser = await dbContext.Users.SingleAsync(u => u.UserId == user.UserId);
        Assert.Equal(NewPasswordHash, updatedUser.PasswordHash);

        var consumedToken = await dbContext.PasswordResetTokens.SingleAsync(t => t.UserId == user.UserId);
        Assert.NotNull(consumedToken.ConsumedAt);

        var revokedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.NotNull(revokedSession.RevokedAt);
        Assert.Equal(SessionRevocationReason.PasswordReset, revokedSession.RevokedReason);

        await revocationStore.Received(1).RevokeAllForUserAsync(user.UserId, FixedNow, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownToken_ThrowsInvalidOrExpired()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await Assert.ThrowsAsync<InvalidOrExpiredPasswordResetTokenException>(
            () => handler.Handle(new ConfirmPasswordResetCommand("no-such-token", NewPassword), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyConsumedToken_ThrowsInvalidOrExpired()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (_, _, resetToken) = await SeedUserWithResetTokenAndLiveSessionAsync(dbContext);
        resetToken.Consume(FixedNow.AddMinutes(-5));
        await dbContext.SaveChangesAsync();
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await Assert.ThrowsAsync<InvalidOrExpiredPasswordResetTokenException>(
            () => handler.Handle(new ConfirmPasswordResetCommand(RawResetToken, NewPassword), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsInvalidOrExpired()
    {
        await using var dbContext = CreateInMemoryDbContext(expiresAt: FixedNow.AddMinutes(-1));
        var handler = CreateHandler(dbContext, Substitute.For<ITokenRevocationStore>());

        await Assert.ThrowsAsync<InvalidOrExpiredPasswordResetTokenException>(
            () => handler.Handle(new ConfirmPasswordResetCommand(RawResetToken, NewPassword), CancellationToken.None));
    }

    private static async Task<(User User, Session Session, PasswordResetToken ResetToken)> SeedUserWithResetTokenAndLiveSessionAsync(
        IdentityDbContext dbContext)
    {
        var user = User.RegisterNative("user@example.com", "old-hash", "Test User", FixedNow);
        var session = Session.CreateNative(user.UserId, FixedNow);
        var resetToken = PasswordResetToken.Issue(user.UserId, HashOf(RawResetToken), FixedNow);

        dbContext.Users.Add(user);
        dbContext.Sessions.Add(session);
        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync();

        return (user, session, resetToken);
    }

    private static string HashOf(string rawToken) => $"hash-of-{rawToken}";

    private static ConfirmPasswordResetCommandHandler CreateHandler(IIdentityDbContext dbContext, ITokenRevocationStore revocationStore)
    {
        var tokenHasher = Substitute.For<ITokenHasher>();
        tokenHasher.Hash(Arg.Any<string>()).Returns(callInfo => HashOf(callInfo.Arg<string>()));

        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(NewPassword).Returns(NewPasswordHash);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new ConfirmPasswordResetCommandHandler(dbContext, tokenHasher, passwordHasher, revocationStore, dateTimeProvider);
    }

    private static IdentityDbContext CreateInMemoryDbContext(DateTimeOffset? expiresAt = null)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dbContext = new IdentityDbContext(options);

        if (expiresAt is not null)
        {
            var user = User.RegisterNative("expired@example.com", "old-hash", "Test User", FixedNow);
            var resetToken = PasswordResetToken.Issue(user.UserId, HashOf(RawResetToken), FixedNow);
            typeof(PasswordResetToken).GetProperty(nameof(PasswordResetToken.ExpiresAt))!.SetValue(resetToken, expiresAt.Value);
            dbContext.Users.Add(user);
            dbContext.PasswordResetTokens.Add(resetToken);
            dbContext.SaveChanges();
        }

        return dbContext;
    }
}
