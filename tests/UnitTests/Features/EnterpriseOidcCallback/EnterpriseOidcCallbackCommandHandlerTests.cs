using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.EnterpriseOidcCallback;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.EnterpriseOidcCallback;

public class EnterpriseOidcCallbackCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string IdpAlias = "azure-ad";
    private static readonly OidcProviderDescriptor OidcProvider = new(
        IdpAlias, "https://login.example.com/authorize", "https://login.example.com/token", "client-id", "client-secret", "https://identity.example.com/oidc/callback", "https://login.example.com", "cert-pem");
    private static readonly EnterpriseIdpDescriptor Idp = new(
        IdpAlias, string.Empty, string.Empty, string.Empty, string.Empty, EnterpriseIdpProtocol.Oidc, OidcProvider);

    [Fact]
    public async Task Handle_FirstLoginForExternalIdentity_JitProvisionsUserAndLinksFederatedIdentity()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com", []));

        var response = await handler.Handle(new EnterpriseOidcCallbackCommand(IdpAlias, "auth-code", "state"), CancellationToken.None);

        var user = await dbContext.Users.SingleAsync();
        Assert.Equal(AccountOrigin.Enterprise, user.AccountOrigin);
        Assert.Null(user.PasswordHash);

        var federatedIdentity = await dbContext.FederatedIdentities.SingleAsync();
        Assert.Equal(user.UserId, federatedIdentity.UserId);
        Assert.Equal(FederatedIdpType.Enterprise, federatedIdentity.IdpType);
        Assert.Equal(IdpAlias, federatedIdentity.IdpKey);
        Assert.Equal("alice-subject", federatedIdentity.ExternalSubjectId);

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
        var federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Enterprise, IdpAlias, "alice-subject", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        dbContext.FederatedIdentities.Add(federatedIdentity);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com", []));
        await handler.Handle(new EnterpriseOidcCallbackCommand(IdpAlias, "auth-code", "state"), CancellationToken.None);

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

        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com", ["Engineering"]));
        var response = await handler.Handle(new EnterpriseOidcCallbackCommand(IdpAlias, "auth-code", "state"), CancellationToken.None);

        Assert.Equal(["support_agent"], response.Roles);
    }

    [Fact]
    public async Task Handle_AssertedGroupWithNoMapping_ResolvesZeroRolesFailClosed()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com", ["SomeUnmappedGroup"]));

        var response = await handler.Handle(new EnterpriseOidcCallbackCommand(IdpAlias, "auth-code", "state"), CancellationToken.None);

        Assert.Empty(response.Roles);
    }

    [Fact]
    public async Task Handle_ExistingUserIsLocked_ThrowsAccountLocked()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = User.ProvisionFederated("alice@example.com", "alice@example.com", AccountOrigin.Enterprise, FixedNow.AddDays(-1));
        user.Lock(FixedNow.AddDays(-1), "some-admin");
        var federatedIdentity = FederatedIdentity.Link(user.UserId, FederatedIdpType.Enterprise, IdpAlias, "alice-subject", FixedNow.AddDays(-1));
        dbContext.Users.Add(user);
        dbContext.FederatedIdentities.Add(federatedIdentity);
        await dbContext.SaveChangesAsync();

        var handler = CreateHandler(dbContext, CreateIdentity("alice-subject", "alice@example.com", []));

        await Assert.ThrowsAsync<AccountLockedException>(
            () => handler.Handle(new EnterpriseOidcCallbackCommand(IdpAlias, "auth-code", "state"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownIdpAlias_ThrowsInvalidOidcToken()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var idpDirectory = Substitute.For<IEnterpriseIdpDirectory>();
        idpDirectory.Find("unknown-idp").Returns((EnterpriseIdpDescriptor?)null);

        var handler = new EnterpriseOidcCallbackCommandHandler(
            dbContext,
            idpDirectory,
            Substitute.For<IOidcTokenExchangeClient>(),
            StubAccessTokenGenerator(),
            StubOpaqueTokenGenerator(),
            StubTokenHasher(),
            StubDateTimeProvider(),
            NullLogger<EnterpriseOidcCallbackCommandHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOidcTokenException>(
            () => handler.Handle(new EnterpriseOidcCallbackCommand("unknown-idp", "auth-code", "state"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_IdpConfiguredForSamlNotOidc_ThrowsInvalidOidcToken()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var samlIdp = new EnterpriseIdpDescriptor(IdpAlias, "https://idp.example.com/sso", "sp-id", "https://identity.example.com/acs", "cert-pem");
        var idpDirectory = Substitute.For<IEnterpriseIdpDirectory>();
        idpDirectory.Find(IdpAlias).Returns(samlIdp);

        var handler = new EnterpriseOidcCallbackCommandHandler(
            dbContext,
            idpDirectory,
            Substitute.For<IOidcTokenExchangeClient>(),
            StubAccessTokenGenerator(),
            StubOpaqueTokenGenerator(),
            StubTokenHasher(),
            StubDateTimeProvider(),
            NullLogger<EnterpriseOidcCallbackCommandHandler>.Instance);

        await Assert.ThrowsAsync<InvalidOidcTokenException>(
            () => handler.Handle(new EnterpriseOidcCallbackCommand(IdpAlias, "auth-code", "state"), CancellationToken.None));
    }

    private static OidcIdentityResult CreateIdentity(string subject, string? email, IReadOnlyCollection<string> groups) =>
        new(subject, email, groups);

    private static EnterpriseOidcCallbackCommandHandler CreateHandler(IIdentityDbContext dbContext, OidcIdentityResult identityResult)
    {
        var idpDirectory = Substitute.For<IEnterpriseIdpDirectory>();
        idpDirectory.Find(IdpAlias).Returns(Idp);

        var tokenExchangeClient = Substitute.For<IOidcTokenExchangeClient>();
        tokenExchangeClient.ExchangeCodeAsync(OidcProvider, Arg.Any<string>(), FixedNow, Arg.Any<CancellationToken>()).Returns(identityResult);

        return new EnterpriseOidcCallbackCommandHandler(
            dbContext,
            idpDirectory,
            tokenExchangeClient,
            StubAccessTokenGenerator(),
            StubOpaqueTokenGenerator(),
            StubTokenHasher(),
            StubDateTimeProvider(),
            NullLogger<EnterpriseOidcCallbackCommandHandler>.Instance);
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
