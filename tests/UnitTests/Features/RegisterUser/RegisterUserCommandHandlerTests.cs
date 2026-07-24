using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.RegisterUser;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.RegisterUser;

public class RegisterUserCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_NewEmail_CreatesAccountSessionAndTokensAndOutboxEvents()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        var response = await handler.Handle(
            new RegisterUserCommand("new.user@example.com", "SuperSecret1", "New User"),
            CancellationToken.None);

        Assert.Equal("minted-access-token", response.AccessToken);
        Assert.Equal("raw-refresh-token", response.RefreshToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(900, response.ExpiresIn);
        Assert.Equal(["customer"], response.Roles);

        var user = await dbContext.Users.SingleAsync();
        Assert.Equal("new.user@example.com", user.Email);
        Assert.Equal("hashed-password", user.PasswordHash);
        Assert.Equal("New User", user.DisplayName);

        var roleGrant = await dbContext.UserRoles.SingleAsync();
        Assert.Equal(user.UserId, roleGrant.UserId);
        Assert.Equal(Domain.Enums.PlatformRole.Customer, roleGrant.Role);

        var session = await dbContext.Sessions.SingleAsync();
        Assert.Equal(user.UserId, session.UserId);
        Assert.False(session.IsFederated);
        Assert.Equal(FixedNow.AddDays(Session.NativeAbsoluteCapDays), session.AbsoluteExpiresAt);

        var refreshToken = await dbContext.RefreshTokens.SingleAsync();
        Assert.Equal(session.SessionId, refreshToken.SessionId);
        Assert.Equal("hashed-refresh-token", refreshToken.TokenHash);
        Assert.Null(refreshToken.ParentTokenId);

        var outboxEventTypes = dbContext.OutboxEvents.Select(e => e.EventType).OrderBy(t => t).ToList();
        Assert.Equal(["SessionCreated", "UserRegistered"], outboxEventTypes);
    }

    [Fact]
    public async Task Handle_DisplayNameOmitted_DefaultsToEmail()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        await handler.Handle(new RegisterUserCommand("no.name@example.com", "SuperSecret1", null), CancellationToken.None);

        var user = await dbContext.Users.SingleAsync();
        Assert.Equal("no.name@example.com", user.DisplayName);
    }

    [Fact]
    public async Task Handle_EmailAlreadyRegistered_ThrowsWithoutCreatingRows()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var now = FixedNow;
        dbContext.Users.Add(User.RegisterNative("taken@example.com", "existing-hash", "Existing", now));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(() =>
            handler.Handle(new RegisterUserCommand("taken@example.com", "SuperSecret1", null), CancellationToken.None));

        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal(0, await dbContext.Sessions.CountAsync());
    }

    private static RegisterUserCommandHandler CreateHandler(IIdentityDbContext dbContext)
    {
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Hash(Arg.Any<string>()).Returns("hashed-password");

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

        return new RegisterUserCommandHandler(
            dbContext,
            passwordHasher,
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
