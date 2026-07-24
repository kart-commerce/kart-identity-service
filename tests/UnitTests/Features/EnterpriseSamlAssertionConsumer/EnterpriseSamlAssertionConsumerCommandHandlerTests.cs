using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.EnterpriseSamlAssertionConsumer;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.EnterpriseSamlAssertionConsumer;

public class EnterpriseSamlAssertionConsumerCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string IdpAlias = "okta-acme";
    private static readonly EnterpriseIdpDescriptor Idp = new(IdpAlias, "https://idp.example.com/sso", "sp-id", "https://identity.example.com/acs", "cert-pem");

    [Fact]
    public async Task Handle_FirstLoginForExternalIdentity_JitProvisionsUserAndLinksFederatedIdentity()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, CreateAssertion("alice@example.com", []));

        var response = await handler.Handle(new EnterpriseSamlAssertionConsumerCommand(IdpAlias, "irrelevant-base64"), CancellationToken.None);

        var user = await dbContext.Users.SingleAsync();
        Assert.Equal(AccountOrigin.Enterprise, user.AccountOrigin);
        Assert.Null(user.PasswordHash);

        var federatedIdentity = await dbContext.FederatedIdentities.SingleAsync();
        Assert.Equal(user.UserId, federatedIdentity.UserId);
        Assert.Equal(FederatedIdpType.Enterprise, federatedIdentity.IdpType);
        Assert.Equal(IdpAlias, federatedIdentity.IdpKey);
        Assert.Equal("alice@example.com", federatedIdentity.ExternalSubjectId);

        Assert.Empty(response.Roles);
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
        var user = User.ProvisionFederated("alice@example.com", "alice@example.com", AccountOrigin.Enterprise, FixedNow.AddDays(-1));
        var federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Enterprise, IdpAlias, "alice@example.com", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        dbContext.FederatedIdentities.Add(federatedIdentity);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, CreateAssertion("alice@example.com", []));
        await handler.Handle(new EnterpriseSamlAssertionConsumerCommand(IdpAlias, "irrelevant-base64"), CancellationToken.None);

        Assert.Equal(1, await dbContext.Users.CountAsync());
        Assert.Equal(1, await dbContext.FederatedIdentities.CountAsync());
        var eventTypes = await dbContext.OutboxEvents.Select(e => e.EventType).ToListAsync();
        Assert.DoesNotContain("UserRegistered", eventTypes);
        Assert.Contains("SessionCreated", eventTypes);
    }

    [Fact]
    public async Task Handle_AssertedGroupWithMapping_ResolvesMappedRole()
    {
        await using var dbContext = CreateInMemoryDbContext();
        dbContext.IdpGroupRoleMappings.Add(IdpGroupRoleMapping.Create(IdpAlias, "Engineering", PlatformRole.SupportAgent, FixedNow, "operator"));
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, CreateAssertion("alice@example.com", ["Engineering"]));
        var response = await handler.Handle(new EnterpriseSamlAssertionConsumerCommand(IdpAlias, "irrelevant-base64"), CancellationToken.None);

        Assert.Equal(["support_agent"], response.Roles);
    }

    [Fact]
    public async Task Handle_AssertedGroupWithNoMapping_ResolvesZeroRolesFailClosed()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, CreateAssertion("alice@example.com", ["SomeUnmappedGroup"]));

        var response = await handler.Handle(new EnterpriseSamlAssertionConsumerCommand(IdpAlias, "irrelevant-base64"), CancellationToken.None);

        Assert.Empty(response.Roles);
    }

    [Fact]
    public async Task Handle_ReplayedAssertion_ThrowsSamlAssertionReplay()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var replayStore = Substitute.For<ISamlAssertionReplayStore>();
        replayStore.TryConsumeAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(false);
        var handler = CreateHandler(dbContext, CreateAssertion("alice@example.com", []), replayStore);

        await Assert.ThrowsAsync<SamlAssertionReplayException>(
            () => handler.Handle(new EnterpriseSamlAssertionConsumerCommand(IdpAlias, "irrelevant-base64"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExistingUserIsLocked_ThrowsAccountLocked()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.ProvisionFederated("alice@example.com", "alice@example.com", AccountOrigin.Enterprise, FixedNow.AddDays(-1));
        user.Lock(FixedNow.AddDays(-1), "some-admin");
        var federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Enterprise, IdpAlias, "alice@example.com", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        dbContext.FederatedIdentities.Add(federatedIdentity);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, CreateAssertion("alice@example.com", []));

        await Assert.ThrowsAsync<AccountLockedException>(
            () => handler.Handle(new EnterpriseSamlAssertionConsumerCommand(IdpAlias, "irrelevant-base64"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownIdpAlias_ThrowsInvalidSamlAssertion()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var idpDirectory = Substitute.For<IEnterpriseIdpDirectory>();
        idpDirectory.Find("unknown-idp").Returns((EnterpriseIdpDescriptor?)null);

        var handler = new EnterpriseSamlAssertionConsumerCommandHandler(
            dbContext,
            idpDirectory,
            Substitute.For<ISamlAssertionValidator>(),
            Substitute.For<ISamlAssertionReplayStore>(),
            StubAccessTokenGenerator(),
            StubOpaqueTokenGenerator(),
            StubTokenHasher(),
            StubDateTimeProvider());

        await Assert.ThrowsAsync<InvalidSamlAssertionException>(
            () => handler.Handle(new EnterpriseSamlAssertionConsumerCommand("unknown-idp", "irrelevant-base64"), CancellationToken.None));
    }

    private static SamlAssertionResult CreateAssertion(string nameId, IReadOnlyCollection<string> groups) =>
        new($"_{Guid.NewGuid():N}", nameId, groups, FixedNow.AddMinutes(5));

    private static EnterpriseSamlAssertionConsumerCommandHandler CreateHandler(
        IIdentityDbContext dbContext, SamlAssertionResult assertionResult, ISamlAssertionReplayStore? replayStore = null)
    {
        var idpDirectory = Substitute.For<IEnterpriseIdpDirectory>();
        idpDirectory.Find(IdpAlias).Returns(Idp);

        var samlAssertionValidator = Substitute.For<ISamlAssertionValidator>();
        samlAssertionValidator.ValidateAndExtract(Arg.Any<string>(), Idp, FixedNow).Returns(assertionResult);

        var effectiveReplayStore = replayStore ?? Substitute.For<ISamlAssertionReplayStore>();
        if (replayStore is null)
        {
            effectiveReplayStore.TryConsumeAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(true);
        }

        return new EnterpriseSamlAssertionConsumerCommandHandler(
            dbContext,
            idpDirectory,
            samlAssertionValidator,
            effectiveReplayStore,
            StubAccessTokenGenerator(),
            StubOpaqueTokenGenerator(),
            StubTokenHasher(),
            StubDateTimeProvider());
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
