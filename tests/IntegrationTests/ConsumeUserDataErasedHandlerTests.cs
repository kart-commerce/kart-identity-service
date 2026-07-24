using System.Net.Http.Json;
using Kart.Identity.Application.Features.ConsumeUserDataErased;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// event-contract.md `UserDataErased` has no HTTP surface in api-contract.yaml —
/// it is consumed asynchronously from `user.exchange` (message-bus-manifest.json),
/// and the RabbitMQ consumer host itself is out of this vertical slice's scope
/// (same "not part of this vertical slice" carve-out as the Outbox poller,
/// `OutboxEvent.cs`). This test instead resolves <see cref="ISender"/> from the
/// same fully-wired DI container (real Sqlite EF, real MediatR pipeline) every
/// HTTP-based integration test uses, sending the command directly — end-to-end
/// coverage of everything except the not-yet-built broker transport.
/// </summary>
public class ConsumeUserDataErasedHandlerTests : IClassFixture<IdentityApiFactory>
{
    private readonly IdentityApiFactory _factory;

    public ConsumeUserDataErasedHandlerTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Consume_ErasesUserAndRevokesLiveSessions()
    {
        var client = _factory.CreateClient();
        var email = $"erasure-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(
            "/v1/auth/register", new { email, password = "SuperSecret1", displayName = "Erase Me" });
        registerResponse.EnsureSuccessStatusCode();
        var userId = await ResolveUserIdAsync(email);

        using (var scope = _factory.Services.CreateScope())
        {
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            await sender.Send(new ConsumeUserDataErasedCommand(userId, DateTimeOffset.UtcNow));
        }

        using var verifyScope = _factory.Services.CreateScope();
        var dbContext = verifyScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var erasedUser = await dbContext.Users.SingleAsync(u => u.UserId == userId);
        Assert.Null(erasedUser.Email);
        Assert.Equal("[erased]", erasedUser.DisplayName);
        Assert.Null(erasedUser.PasswordHash);

        var session = await dbContext.Sessions.SingleAsync(s => s.UserId == userId);
        Assert.NotNull(session.RevokedAt);
        Assert.Equal(SessionRevocationReason.Erasure, session.RevokedReason);
    }

    private async Task<Guid> ResolveUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Email == email);
        return user.UserId;
    }
}
