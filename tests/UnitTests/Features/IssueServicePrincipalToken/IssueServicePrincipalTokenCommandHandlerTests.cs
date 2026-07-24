using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.IssueServicePrincipalToken;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.IssueServicePrincipalToken;

public class IssueServicePrincipalTokenCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private const string ClientId = "admin-service";
    private const string StoredHash = "stored-hash";
    private const string CorrectSecret = "CorrectSecret1";

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsScopedAccessTokenWithNoRefreshToken()
    {
        await using var dbContext = CreateInMemoryDbContext();
        SeedPrincipal(dbContext, PlatformRole.Admin, ServicePrincipalStatus.Active);
        var handler = CreateHandler(dbContext);

        var response = await handler.Handle(
            new IssueServicePrincipalTokenCommand("client_credentials", ClientId, CorrectSecret, "internal.lock internal.unlock"),
            CancellationToken.None);

        Assert.Equal("minted-access-token", response.AccessToken);
        Assert.Equal("Bearer", response.TokenType);
        Assert.Equal(900, response.ExpiresIn);
        Assert.Equal(["internal.lock", "internal.unlock"], response.Scopes);
    }

    [Fact]
    public async Task Handle_NoScopeRequested_ReturnsEmptyScopes()
    {
        await using var dbContext = CreateInMemoryDbContext();
        SeedPrincipal(dbContext, PlatformRole.PartnerApi, ServicePrincipalStatus.Active);
        var handler = CreateHandler(dbContext);

        var response = await handler.Handle(
            new IssueServicePrincipalTokenCommand("client_credentials", ClientId, CorrectSecret, null),
            CancellationToken.None);

        Assert.Empty(response.Scopes);
    }

    [Fact]
    public async Task Handle_UnknownClientId_Throws()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<InvalidServicePrincipalCredentialsException>(() =>
            handler.Handle(new IssueServicePrincipalTokenCommand("client_credentials", "no-such-client", CorrectSecret, null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongSecret_Throws()
    {
        await using var dbContext = CreateInMemoryDbContext();
        SeedPrincipal(dbContext, PlatformRole.Admin, ServicePrincipalStatus.Active);
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<InvalidServicePrincipalCredentialsException>(() =>
            handler.Handle(new IssueServicePrincipalTokenCommand("client_credentials", ClientId, "WrongSecret", null), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_RevokedPrincipal_ThrowsEvenWithCorrectSecret()
    {
        await using var dbContext = CreateInMemoryDbContext();
        SeedPrincipal(dbContext, PlatformRole.Admin, ServicePrincipalStatus.Revoked);
        var handler = CreateHandler(dbContext);

        await Assert.ThrowsAsync<InvalidServicePrincipalCredentialsException>(() =>
            handler.Handle(new IssueServicePrincipalTokenCommand("client_credentials", ClientId, CorrectSecret, null), CancellationToken.None));
    }

    private static void SeedPrincipal(IdentityDbContext dbContext, PlatformRole role, ServicePrincipalStatus status)
    {
        var principal = ServicePrincipal.Provision(ClientId, StoredHash, role, FixedNow, "test-seed");
        if (status == ServicePrincipalStatus.Revoked)
        {
            typeof(ServicePrincipal).GetProperty(nameof(ServicePrincipal.Status))!.SetValue(principal, ServicePrincipalStatus.Revoked);
        }

        dbContext.ServicePrincipals.Add(principal);
        dbContext.SaveChanges();
    }

    private static IssueServicePrincipalTokenCommandHandler CreateHandler(IIdentityDbContext dbContext)
    {
        var passwordHasher = Substitute.For<IPasswordHasher>();
        passwordHasher.Verify(Arg.Any<string>(), Arg.Any<string?>()).Returns(false);
        passwordHasher.Verify(CorrectSecret, StoredHash).Returns(true);

        var accessTokenGenerator = Substitute.For<IAccessTokenGenerator>();
        accessTokenGenerator
            .Generate(Arg.Any<string>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<IReadOnlyCollection<string>>())
            .Returns(new AccessToken("minted-access-token", 900));

        return new IssueServicePrincipalTokenCommandHandler(dbContext, passwordHasher, accessTokenGenerator);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
