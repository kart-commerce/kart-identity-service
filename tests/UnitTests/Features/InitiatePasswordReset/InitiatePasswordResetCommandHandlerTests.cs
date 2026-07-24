using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.InitiatePasswordReset;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.InitiatePasswordReset;

public class InitiatePasswordResetCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string RawResetToken = "raw-reset-token";

    [Fact]
    public async Task Handle_ExistingAccount_CreatesPasswordResetTokenRow()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("user@example.com", "hash", "Test User", FixedNow);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        await handler.Handle(new InitiatePasswordResetCommand("user@example.com"), CancellationToken.None);

        var resetToken = await dbContext.PasswordResetTokens.SingleAsync(t => t.UserId == user.UserId);
        Assert.Equal(HashOf(RawResetToken), resetToken.TokenHash);
        Assert.Equal(FixedNow.AddMinutes(PasswordResetToken.ValidityMinutes), resetToken.ExpiresAt);
        Assert.Null(resetToken.ConsumedAt);
    }

    [Fact]
    public async Task Handle_UnknownAccount_DoesNotCreateAnyRowAndDoesNotThrow()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        await handler.Handle(new InitiatePasswordResetCommand("no-such-user@example.com"), CancellationToken.None);

        Assert.Empty(dbContext.PasswordResetTokens);
    }

    private static string HashOf(string rawToken) => $"hash-of-{rawToken}";

    private static InitiatePasswordResetCommandHandler CreateHandler(IIdentityDbContext dbContext)
    {
        var opaqueTokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
        opaqueTokenGenerator.Generate().Returns(RawResetToken);

        var tokenHasher = Substitute.For<ITokenHasher>();
        tokenHasher.Hash(Arg.Any<string>()).Returns(callInfo => HashOf(callInfo.Arg<string>()));

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new InitiatePasswordResetCommandHandler(dbContext, opaqueTokenGenerator, tokenHasher, dateTimeProvider);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
