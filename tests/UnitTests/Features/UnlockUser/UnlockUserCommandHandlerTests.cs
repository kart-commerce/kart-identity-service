using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.UnlockUser;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.UnlockUser;

public class UnlockUserCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string UnlockedBy = "admin-service-principal";

    [Fact]
    public async Task Handle_LockedUser_ClearsLockedAtAndLockedBy()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("user@example.com", "hash", "Test User", FixedNow);
        user.Lock(FixedNow.AddMinutes(-10), "some-admin");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        await handler.Handle(new UnlockUserCommand(user.UserId.ToString(), UnlockedBy), CancellationToken.None);

        var unlockedUser = await dbContext.Users.SingleAsync(u => u.UserId == user.UserId);
        Assert.Null(unlockedUser.LockedAt);
        Assert.Null(unlockedUser.LockedBy);
        Assert.Equal(UnlockedBy, unlockedUser.UpdatedBy);
    }

    [Fact]
    public async Task Handle_UnknownUserId_ThrowsUserNotFound()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => handler.Handle(new UnlockUserCommand(Guid.NewGuid().ToString(), UnlockedBy), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MalformedUserId_ThrowsUserNotFound()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => handler.Handle(new UnlockUserCommand("not-a-guid", UnlockedBy), CancellationToken.None));
    }

    private static UnlockUserCommandHandler CreateHandler(IIdentityDbContext dbContext)
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);
        return new UnlockUserCommandHandler(dbContext, dateTimeProvider);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
