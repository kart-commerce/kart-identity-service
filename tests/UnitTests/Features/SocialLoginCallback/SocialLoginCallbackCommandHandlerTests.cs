using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.SocialLoginCallback;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.SocialLoginCallback;

public class SocialLoginCallbackCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string Provider = "google";
    private static readonly OidcProviderDescriptor SocialProvider = new(
        Provider, "https://accounts.google.com/o/oauth2/auth", "https://oauth2.googleapis.com/token",
        "client-id", "client-secret", "https://identity.example.com/social/callback", "https://accounts.google.com", "cert-pem");

    [Fact]
    public async Task Handle_FirstLoginForExternalIdentity_JitProvisionsCustomerAccount()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com"));

        var response = await handler.Handle(new SocialLoginCallbackCommand(Provider, "auth-code", "state"), CancellationToken.None);

        var user = await dbContext.Users.SingleAsync();
        Assert.Equal(AccountOrigin.Social, user.AccountOrigin);
        Assert.Null(user.PasswordHash);

        var federatedIdentity = await dbContext.FederatedIdentities.SingleAsync();
        Assert.Equal(FederatedIdpType.Social, federatedIdentity.IdpType);
        Assert.Equal(Provider, federatedIdentity.IdpKey);
        Assert.Equal("alice-subject", federatedIdentity.ExternalSubjectId);

        var roleGrant = await dbContext.UserRoles.SingleAsync();
        Assert.Equal(PlatformRole.Customer, roleGrant.Role);
        Assert.Equal("social-jit", roleGrant.GrantedBy);

        Assert.Equal(["customer"], response.Roles);
        Assert.False(string.IsNullOrEmpty(response.AccessToken));
        Assert.False(string.IsNullOrEmpty(response.RefreshToken));

        var eventTypes = await dbContext.OutboxEvents.Select(e => e.EventType).ToListAsync();
        Assert.Contains("UserRegistered", eventTypes);
        Assert.Contains("SessionCreated", eventTypes);
    }

    [Fact]
    public async Task Handle_SecondLoginForSameExternalIdentity_ReusesExistingUserAndDoesNotRepublishUserRegistered()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.ProvisionFederated("alice@example.com", "alice@example.com", AccountOrigin.Social, FixedNow.AddDays(-1));
        var federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Social, Provider, "alice-subject", FixedNow.AddDays(-1));
        var roleGrant = UserRole.Grant(user.UserId, PlatformRole.Customer, "social-jit", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        dbContext.FederatedIdentities.Add(federatedIdentity);
        dbContext.UserRoles.Add(roleGrant);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com"));
        var response = await handler.Handle(new SocialLoginCallbackCommand(Provider, "auth-code", "state"), CancellationToken.None);

        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.FederatedIdentities.CountAsync());
        Assert.Equal(1, await dbContext.UserRoles.CountAsync());
        Assert.Equal(["customer"], response.Roles);
        var eventTypes = await dbContext.OutboxEvents.Select(e => e.EventType).ToListAsync();
        Assert.DoesNotContain("UserRegistered", eventTypes);
        Assert.Contains("SessionCreated", eventTypes);
    }

    [Fact]
    public async Task Handle_ExistingUserIsLocked_ThrowsAccountLocked()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.ProvisionFederated("alice@example.com", "alice@example.com", AccountOrigin.Social, FixedNow.AddDays(-1));
        user.Lock(FixedNow.AddDays(-1), "some-admin");
        var federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Social, Provider, "alice-subject", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        dbContext.FederatedIdentities.Add(federatedIdentity);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com"));

        await Assert.ThrowsAsync<AccountLockedException>(
            () => handler.Handle(new SocialLoginCallbackCommand(Provider, "auth-code", "state"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownProvider_ThrowsInvalidOidcToken()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var socialIdpDirectory = Substitute.For<ISocialIdpDirectory>();
        socialIdpDirectory.Find("unknown-provider").Returns((OidcProviderDescriptor?)null);

        var handler = new SocialLoginCallbackCommandHandler(
            dbContext,
            socialIdpDirectory,
            Substitute.For<IOidcTokenExchangeClient>(),
            StubAccessTokenGenerator(),
            StubOpaqueTokenGenerator(),
            StubTokenHasher(),
            StubDateTimeProvider(),
            NullLogger<SocialLoginCallbackCommandHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOidcTokenException>(
            () => handler.Handle(new SocialLoginCallbackCommand("unknown-provider", "auth-code", "state"), CancellationToken.None));
    }

    private static OidcIdentityResult CreateIdentity(string subject, string? email) => new(subject, email, []);

    private static SocialLoginCallbackCommandHandler CreateHandler(IIdentityDbContext dbContext, OidcIdentityResult identityResult)
    {
        var socialIdpDirectory = Substitute.For<ISocialIdpDirectory>();
        socialIdpDirectory.Find(Provider).Returns(SocialProvider);

        var tokenExchangeClient = Substitute.For<IOidcTokenExchangeClient>();
        tokenExchangeClient.ExchangeCodeAsync(SocialProvider, Arg.Any<string>(), FixedNow, Arg.Any<CancellationToken>()).Returns(identityResult);

        return new SocialLoginCallbackCommandHandler(
            dbContext,
            socialIdpDirectory,
            tokenExchangeClient,
            StubAccessTokenGenerator(),
            StubOpaqueTokenGenerator(),
            StubTokenHasher(),
            StubDateTimeProvider(),
            NullLogger<SocialLoginCallbackCommandHandler>.Instance);
    }

    private static IAccessTokenGenerator StubAccessTokenGenerator()
    {
        var accessTokenGenerator = Substitute.For<IAccessTokenGenerator>();
        accessTokenGenerator
            .Generate(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new AccessToken("minted-access-token", 900));
        return accessTokenGenerator;
    }

    private static IOpaqueTokenGenerator StubOpaqueTokenGenerator()
    {
        var opaqueTokenGenerator = Substitute.For<IOpaqueTokenGenerator>();
        opaqueTokenGenerator.Generate().Returns("raw-refresh-token");
        return opaqueTokenGenerator;
    }

    private static ITokenHasher StubTokenHasher()
    {
        var tokenHasher = Substitute.For<ITokenHasher>();
        tokenHasher.Hash(Arg.Any<string>()).Returns(callInfo => $"hash-of-{callInfo.Arg<string>()}");
        return tokenHasher;
    }

    private static IDateTimeProvider StubDateTimeProvider()
    {
        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);
        return dateTimeProvider;
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
