using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.RotateRefreshToken;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.RotateRefreshToken;

public class RotateRefreshTokenCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string RawOldToken = "raw-old-token";
    private const string RawNewToken = "raw-new-token";

    [Fact]
    public async Task Handle_LiveUnconsumedToken_RotatesAndReturnsNewPair()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (user, session, _) = await SeedSessionWithLiveTokenAsync(dbContext);
        await dbContext.UserRoles.AddAsync(UserRole.Grant(user.UserId, PlatformRole.Customer, "test-seed", FixedNow));
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        var response = await handler.Handle(new RotateRefreshTokenCommand(RawOldToken), CancellationToken.None);

        Assert.Equal("minted-access-token", response.AccessToken);
        Assert.Equal(RawNewToken, response.RefreshToken);
        Assert.Equal(["customer"], response.Roles);

        var oldToken = await dbContext.RefreshTokens.SingleAsync(t => t.SessionId == session.SessionId && t.ParentTokenId == null);
        Assert.NotNull(oldToken.ConsumedAt);
        Assert.NotNull(oldToken.ReplacedByTokenId);

        var newToken = await dbContext.RefreshTokens.SingleAsync(t => t.TokenId == oldToken.ReplacedByTokenId);
        Assert.Null(newToken.ConsumedAt);
        Assert.Equal(oldToken.TokenId, newToken.ParentTokenId);
    }

    [Fact]
    public async Task Handle_UnknownToken_ThrowsReuseDetected()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<RefreshTokenReuseDetectedException>(
            () => handler.Handle(new RotateRefreshTokenCommand("no-such-token"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyConsumedToken_ThrowsReuseDetectedAndRevokesSession()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (_, session, oldToken) = await SeedSessionWithLiveTokenAsync(dbContext);
        oldToken.Consume(FixedNow.AddMinutes(-5), Guid.NewGuid(), "someone");
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<RefreshTokenReuseDetectedException>(
            () => handler.Handle(new RotateRefreshTokenCommand(RawOldToken), CancellationToken.None));

        var revokedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.NotNull(revokedSession.RevokedAt);
        Assert.Equal(SessionRevocationReason.ReuseDetected, revokedSession.RevokedReason);
    }

    [Fact]
    public async Task Handle_SessionAlreadyRevokedForAnotherReason_ThrowsWithoutOverwritingRevocationReason()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (_, session, _) = await SeedSessionWithLiveTokenAsync(dbContext);
        session.Revoke(SessionRevocationReason.Logout, FixedNow.AddMinutes(-10), "the-user");
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<RefreshTokenReuseDetectedException>(
            () => handler.Handle(new RotateRefreshTokenCommand(RawOldToken), CancellationToken.None));

        var unchangedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Equal(SessionRevocationReason.Logout, unchangedSession.RevokedReason);
    }

    [Fact]
    public async Task Handle_ExpiredNeverConsumedToken_ThrowsWithoutRevokingSession()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var (_, session, oldToken) = await SeedSessionWithLiveTokenAsync(dbContext, tokenExpiresAt: FixedNow.AddMinutes(-1));

        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<RefreshTokenReuseDetectedException>(
            () => handler.Handle(new RotateRefreshTokenCommand(RawOldToken), CancellationToken.None));

        var untouchedSession = await dbContext.Sessions.SingleAsync(s => s.SessionId == session.SessionId);
        Assert.Null(untouchedSession.RevokedAt);
        var untouchedToken = await dbContext.RefreshTokens.SingleAsync(t => t.TokenId == oldToken.TokenId);
        Assert.Null(untouchedToken.ConsumedAt);
    }

    [Fact]
    public async Task Handle_ConcurrentRotationOfTheSameToken_LoserThrowsRaceLost()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var seedContext = CreateInMemoryDbContext(dbName);
        var (user, session, _) = await SeedSessionWithLiveTokenAsync(seedContext);

        await using var dbContext1 = CreateInMemoryDbContext(dbName);
        await using var dbContext2 = CreateInMemoryDbContext(dbName);

        // Both contexts observe the token as still-live BEFORE either rotates —
        // this is what a genuine concurrent race looks like: two requests read
        // "not yet consumed" before either write commits.
        _ = await dbContext1.RefreshTokens.SingleAsync(t => t.SessionId == session.SessionId);
        _ = await dbContext2.RefreshTokens.SingleAsync(t => t.SessionId == session.SessionId);

        // Distinct new-token values per handler — otherwise a would-be duplicate
        // `token_hash` insert from the loser could surface as a unique-constraint
        // failure instead of the concurrency conflict this test is isolating.
        var handler1 = CreateHandler(dbContext1, newRawToken: RawNewToken);
        var handler2 = CreateHandler(dbContext2, newRawToken: "raw-new-token-loser");

        var winnerResponse = await handler1.Handle(new RotateRefreshTokenCommand(RawOldToken), CancellationToken.None);
        Assert.Equal(RawNewToken, winnerResponse.RefreshToken);

        await Assert.ThrowsAsync<RefreshTokenRaceLostException>(
            () => handler2.Handle(new RotateRefreshTokenCommand(RawOldToken), CancellationToken.None));
    }

    private static async Task<(User User, Session Session, RefreshToken Token)> SeedSessionWithLiveTokenAsync(
        IdentityDbContext dbContext, DateTimeOffset? tokenExpiresAt = null)
    {
        var user = User.RegisterNative("user@example.com", "hash", "Test User", FixedNow);
        var session = Session.CreateNative(user.UserId, FixedNow);
        var token = RefreshToken.IssueInitial(session.SessionId, HashOf(RawOldToken), FixedNow, session.AbsoluteExpiresAt, user.UserId.ToString());

        dbContext.Users.Add(user);
        dbContext.Sessions.Add(session);
        dbContext.RefreshTokens.Add(token);
        await dbContext.SaveChangesAsync();

        if (tokenExpiresAt is not null)
        {
            typeof(RefreshToken).GetProperty(nameof(RefreshToken.ExpiresAt))!.SetValue(token, tokenExpiresAt.Value);
            await dbContext.SaveChangesAsync();
        }

        return (user, session, token);
    }

    private static string HashOf(string rawToken) => $"hash-of-{rawToken}";

    private static RotateRefreshTokenCommandHandler CreateHandler(IIdentityDbContext dbContext, string newRawToken = RawNewToken)
    {
        var accessTokenGenerator = Substitute.For<IAccessTokenGenerator>();
        accessTokenGenerator
            .Generate(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new AccessToken("minted-access-token", 900));

        var opaqueTokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
        opaqueTokenGenerator.Generate().Returns(newRawToken);

        var tokenHasher = Substitute.For<ITokenHasher>();
        tokenHasher.Hash(Arg.Any<string>()).Returns(callInfo => HashOf(callInfo.Arg<string>()));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new RotateRefreshTokenCommandHandler(dbContext, accessTokenGenerator, opaqueTokenGenerator, tokenHasher, dateTimeProvider);
    }

    private static IdentityDbContext CreateInMemoryDbContext(string? sharedName = null)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(sharedName ?? Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
