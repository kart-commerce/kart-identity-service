using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.Login;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.Login;

public class LoginCommandHandlerTests
{
    private const string StoredHash = "stored-hash";
    private const string CorrectPassword = "CorrectPassword1";
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsAuthenticatedResultAndCreatesSessionAndRefreshToken()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = await SeedUserWithRoleAsync(dbContext, "user@example.com", PlatformRole.Customer);

        var loginAttemptThrottle = Substitute.For<ILoginAttemptThrottle>();
        var handler = CreateHandler(dbContext, loginAttemptThrottle: loginAttemptThrottle);

        var result = await handler.Handle(
            new LoginCommand("user@example.com", CorrectPassword, "203.0.113.1"),
            CancellationToken.None);

        var authenticated = Assert.IsType<AuthenticatedLoginResult>(result);
        Assert.Equal("minted-access-token", authenticated.AccessToken);
        Assert.Equal("raw-refresh-token", authenticated.RefreshToken);
        Assert.Equal("Bearer", authenticated.TokenType);
        Assert.Equal(900, authenticated.ExpiresIn);
        Assert.Equal(["customer"], authenticated.Roles);

        Assert.Equal(1, await dbContext.Sessions.CountAsync(s => s.UserId == user.UserId));
        Assert.Equal(1, await dbContext.RefreshTokens.CountAsync());
        Assert.Equal("SessionCreated", (await dbContext.OutboxEvents.SingleAsync()).EventType);

        await loginAttemptThrottle.Received(1).ResetAsync("user@example.com", "203.0.113.1", Arg.Any<CancellationToken>());
        await loginAttemptThrottle.DidNotReceive().RecordFailureAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownEmail_RecordsFailureAndThrowsInvalidCredentials()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var loginAttemptThrottle = Substitute.For<ILoginAttemptThrottle>();
        var handler = CreateHandler(dbContext, loginAttemptThrottle: loginAttemptThrottle);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.Handle(new LoginCommand("nobody@example.com", "whatever", "203.0.113.1"), CancellationToken.None));

        await loginAttemptThrottle.Received(1).RecordFailureAsync("nobody@example.com", "203.0.113.1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WrongPassword_RecordsFailureAndThrowsInvalidCredentials()
    {
        await using var dbContext = CreateInMemoryDbContext();
        await SeedUserWithRoleAsync(dbContext, "user@example.com", PlatformRole.Customer);
        var loginAttemptThrottle = Substitute.For<ILoginAttemptThrottle>();
        var handler = CreateHandler(dbContext, loginAttemptThrottle: loginAttemptThrottle);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            handler.Handle(new LoginCommand("user@example.com", "WrongPassword", "203.0.113.1"), CancellationToken.None));

        await loginAttemptThrottle.Received(1).RecordFailureAsync("user@example.com", "203.0.113.1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ThrottleBlocked_ThrowsRateLimitWithoutVerifyingPassword()
    {
        await using var dbContext = CreateInMemoryDbContext();
        await SeedUserWithRoleAsync(dbContext, "user@example.com", PlatformRole.Customer);

        var loginAttemptThrottle = Substitute.For<ILoginAttemptThrottle>();
        loginAttemptThrottle.IsBlockedAsync("user@example.com", "203.0.113.1", Arg.Any<CancellationToken>()).Returns(true);
        var passwordHasher = Substitute.For<IPasswordHasher>();
        var handler = CreateHandler(dbContext, passwordHasher: passwordHasher, loginAttemptThrottle: loginAttemptThrottle);

        await Assert.ThrowsAsync<LoginRateLimitExceededException>(() =>
            handler.Handle(new LoginCommand("user@example.com", CorrectPassword, "203.0.113.1"), CancellationToken.None));

        passwordHasher.DidNotReceiveWithAnyArgs().Verify(default!, default);
    }

    [Fact]
    public async Task Handle_LockedAccount_ThrowsAccountLocked()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = await SeedUserWithRoleAsync(dbContext, "locked@example.com", PlatformRole.Customer);
        // No admin-lock endpoint exists yet (IDN-10) — reflection is the only way
        // to put a row into the locked state this handler must already defend
        // against reading.
        await LockUserAsync(dbContext, user.UserId);

        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<AccountLockedException>(() =>
            handler.Handle(new LoginCommand("locked@example.com", CorrectPassword, "203.0.113.1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AdminRole_ReturnsMfaChallengeAndDoesNotCreateSession()
    {
        await using var dbContext = CreateInMemoryDbContext();
        await SeedUserWithRoleAsync(dbContext, "admin@example.com", PlatformRole.Admin);

        var mfaChallengeStore = Substitute.For<IMfaChallengeStore>();
        mfaChallengeStore
            .CreateAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new MfaChallenge("challenge-token", 300));
        var handler = CreateHandler(dbContext, mfaChallengeStore: mfaChallengeStore);

        var result = await handler.Handle(
            new LoginCommand("admin@example.com", CorrectPassword, "203.0.113.1"),
            CancellationToken.None);

        var challenge = Assert.IsType<MfaChallengeLoginResult>(result);
        Assert.Equal("challenge-token", challenge.ChallengeId);
        Assert.Equal(300, challenge.ExpiresInSeconds);
        Assert.Equal(0, await dbContext.Sessions.CountAsync());
    }

    private static async Task<User> SeedUserWithRoleAsync(IdentityDbContext dbContext, string email, PlatformRole role)
    {
        var user = User.RegisterNative(email, StoredHash, "Test User", FixedNow);
        dbContext.Users.Add(user);
        // Grant() covers both native self-registration's Customer default and the
        // Admin/SupportAgent grants this test seeds directly — no public endpoint
        // creates those yet (database-design.md's out-of-band elevation note).
        dbContext.UserRoles.Add(UserRole.Grant(user.UserId, role, grantedBy: "test-seed", FixedNow));
        await dbContext.SaveChangesAsync(CancellationToken.None);
        return user;
    }

    private static async Task LockUserAsync(IdentityDbContext dbContext, Guid userId)
    {
        var user = await dbContext.Users.SingleAsync(u => u.UserId == userId);
        var lockedAtProperty = typeof(User).GetProperty(nameof(User.LockedAt))!;
        lockedAtProperty.SetValue(user, FixedNow);
        await dbContext.SaveChangesAsync(CancellationToken.None);
    }

    private static LoginCommandHandler CreateHandler(
        IIdentityDbContext dbContext,
        IPasswordHasher? passwordHasher = null,
        ILoginAttemptThrottle? loginAttemptThrottle = null,
        IMfaChallengeStore? mfaChallengeStore = null)
    {
        passwordHasher ??= CreateDefaultPasswordHasher();

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

        loginAttemptThrottle ??= Substitute.For<ILoginAttemptThrottle>();
        mfaChallengeStore ??= Substitute.For<IMfaChallengeStore>();

        return new LoginCommandHandler(
            dbContext,
            passwordHasher,
            accessTokenGenerator,
            opaqueTokenGenerator,
            tokenHasher,
            dateTimeProvider,
            loginAttemptThrottle,
            mfaChallengeStore,
            NullLogger<LoginCommandHandler>.Instance);
    }

    private static IPasswordHasher CreateDefaultPasswordHasher()
    {
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);
        passwordHasher.Verify(CorrectPassword, StoredHash).Returns(true);
        return passwordHasher;
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
