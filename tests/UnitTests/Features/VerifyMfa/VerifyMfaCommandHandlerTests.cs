using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.VerifyMfa;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.VerifyMfa;

public class VerifyMfaCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ValidChallengeAndCode_MintsTokensAndCreatesSession()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = SeedUser(dbContext);
        SeedActiveCredential(dbContext, user.UserId);

        var mfaChallengeStore = Substitute.For<IMfaChallengeStore>();
        mfaChallengeStore.GetAndConsumeAsync("challenge-id", Arg.Any<CancellationToken>())
            .Returns(new MfaChallengeState(user.UserId, ["admin"]));

        var handler = CreateHandler(dbContext, mfaChallengeStore, codeIsValid: true);

        var response = await handler.Handle(new VerifyMfaCommand("challenge-id", "123456"), CancellationToken.None);

        Assert.Equal("minted-access-token", response.AccessToken);
        Assert.Equal("raw-refresh-token", response.RefreshToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(900, response.ExpiresIn);
        Assert.Equal(["admin"], response.Roles);
        Assert.Equal(1, await dbContext.Sessions.CountAsync(s => s.UserId == user.UserId));
        Assert.Equal(1, await dbContext.RefreshTokens.CountAsync());
        Assert.Equal("SessionCreated", (await dbContext.OutboxEvents.SingleAsync()).EventType);
    }

    [Fact]
    public async Task Handle_UnknownOrExpiredChallenge_ThrowsWithoutCreatingSession()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var mfaChallengeStore = Substitute.For<IMfaChallengeStore>();
        mfaChallengeStore.GetAndConsumeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((MfaChallengeState?)null);

        var handler = CreateHandler(dbContext, mfaChallengeStore, codeIsValid: true);

        await Assert.ThrowsAsync<InvalidMfaChallengeException>(
            () => handler.Handle(new VerifyMfaCommand("bad-challenge", "123456"), CancellationToken.None));

        Assert.Equal(0, await dbContext.Sessions.CountAsync());
    }

    [Fact]
    public async Task Handle_NoActiveCredentialForChallengeOwner_Throws()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = SeedUser(dbContext);
        // No mfa_credentials row at all — the flagged Login gap this ticket doesn't
        // need to resolve, but VerifyMfa must still fail safely rather than crash.

        var mfaChallengeStore = Substitute.For<IMfaChallengeStore>();
        mfaChallengeStore.GetAndConsumeAsync("challenge-id", Arg.Any<CancellationToken>())
            .Returns(new MfaChallengeState(user.UserId, ["admin"]));

        var handler = CreateHandler(dbContext, mfaChallengeStore, codeIsValid: true);

        await Assert.ThrowsAsync<InvalidMfaChallengeException>(
            () => handler.Handle(new VerifyMfaCommand("challenge-id", "123456"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongCode_ThrowsWithoutCreatingSession()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = SeedUser(dbContext);
        SeedActiveCredential(dbContext, user.UserId);

        var mfaChallengeStore = Substitute.For<IMfaChallengeStore>();
        mfaChallengeStore.GetAndConsumeAsync("challenge-id", Arg.Any<CancellationToken>())
            .Returns(new MfaChallengeState(user.UserId, ["admin"]));

        var handler = CreateHandler(dbContext, mfaChallengeStore, codeIsValid: false);

        await Assert.ThrowsAsync<InvalidMfaChallengeException>(
            () => handler.Handle(new VerifyMfaCommand("challenge-id", "000000"), CancellationToken.None));

        Assert.Equal(0, await dbContext.Sessions.CountAsync());
    }

    private static User SeedUser(IdentityDbContext dbContext)
    {
        var user = User.RegisterNative("admin@example.com", "hash", "Admin User", FixedNow);
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user;
    }

    private static void SeedActiveCredential(IdentityDbContext dbContext, Guid userId)
    {
        var credential = MfaCredential.BeginEnrollment(userId, [0xAA, 0xBB], FixedNow.AddMinutes(-10), TimeSpan.FromMinutes(10));
        credential.Confirm(FixedNow.AddMinutes(-5));
        dbContext.MfaCredentials.Add(credential);
        dbContext.SaveChanges();
    }

    private static VerifyMfaCommandHandler CreateHandler(IIdentityDbContext dbContext, IMfaChallengeStore mfaChallengeStore, bool codeIsValid)
    {
        var mfaSecretCipher = Substitute.For<IMfaSecretCipher>();
        mfaSecretCipher.Decrypt(Arg.Any<byte[]>()).Returns("BASE32SECRET");

        var totpCodeValidator = Substitute.For<ITotpCodeValidator>();
        totpCodeValidator.IsCodeValid(Arg.Any<string>(), Arg.Any<string>()).Returns(codeIsValid);

        var accessTokenGenerator = Substitute.For<IAccessTokenGenerator>();
        accessTokenGenerator
            .Generate(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new AccessToken("minted-access-token", 900));

        var opaqueTokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
        opaqueTokenGenerator.Generate().Returns("raw-refresh-token");

        var tokenHasher = Substitute.For<ITokenHasher>();
        tokenHasher.Hash(Arg.Any<string>()).Returns("hashed-refresh-token");

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new VerifyMfaCommandHandler(
            dbContext,
            mfaChallengeStore,
            mfaSecretCipher,
            totpCodeValidator,
            accessTokenGenerator,
            opaqueTokenGenerator,
            tokenHasher,
            dateTimeProvider);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
