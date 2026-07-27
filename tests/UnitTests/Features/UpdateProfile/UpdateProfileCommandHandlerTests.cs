using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.UpdateProfile;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.UpdateProfile;

public class UpdateProfileCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_UpdatesEmailAndDisplayName_PersistsAndPublishesUserAccountUpdated()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("old@example.com", "hash", "Old Name", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var response = await handler.Handle(new UpdateProfileCommand(user.UserId, "new@example.com", "New Name"), CancellationToken.None);

        Assert.Equal("new@example.com", response.Email);
        Assert.Equal("New Name", response.DisplayName);
        Assert.Equal(FixedNow, response.UpdatedAt);

        var persisted = await dbContext.Users.SingleAsync();
        Assert.Equal("new@example.com", persisted.Email);
        Assert.Equal("New Name", persisted.DisplayName);

        var outboxEvent = await dbContext.OutboxEvents.SingleAsync();
        Assert.Equal("UserAccountUpdated", outboxEvent.EventType);
        Assert.Contains("new@example.com", outboxEvent.Payload);
    }

    [Fact]
    public async Task Handle_OnlyDisplayNameSupplied_LeavesEmailUnchanged()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("keep@example.com", "hash", "Old Name", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);
        var response = await handler.Handle(new UpdateProfileCommand(user.UserId, null, "New Name"), CancellationToken.None);

        Assert.Equal("keep@example.com", response.Email);
        Assert.Equal("New Name", response.DisplayName);
    }

    [Fact]
    public async Task Handle_EmailAlreadyRegisteredToAnotherAccount_ThrowsEmailAlreadyRegistered()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.RegisterNative("mine@example.com", "hash", "Mine", FixedNow.AddDays(-1));
        var otherUser = User.RegisterNative("taken@example.com", "hash", "Other", FixedNow.AddDays(-1));
        dbContext.Users.AddRange(user, otherUser);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<EmailAlreadyRegisteredException>(
            () => handler.Handle(new UpdateProfileCommand(user.UserId, "taken@example.com", null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownUserId_ThrowsUserNotFound()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => handler.Handle(new UpdateProfileCommand(Guid.NewGuid(), "new@example.com", null), CancellationToken.None));
    }

    private static UpdateProfileCommandHandler CreateHandler(IIdentityDbContext dbContext)
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);
        return new UpdateProfileCommandHandler(dbContext, dateTimeProvider, NullLogger<UpdateProfileCommandHandler>.Instance);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
