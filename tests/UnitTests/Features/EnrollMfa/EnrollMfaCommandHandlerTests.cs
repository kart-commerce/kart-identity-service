using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Application.Features.EnrollMfa;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.EnrollMfa;

public class EnrollMfaCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_NewEnrollment_CreatesPendingCredentialAndReturnsProvisioningUri()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = SeedUser(dbContext, "user@example.com");

        var handler = CreateHandler(dbContext);

        var response = await handler.Handle(new EnrollMfaCommand(user.UserId), CancellationToken.None);

        Assert.Equal("otpauth://totp/fake-uri", response.ProvisioningUri);
        Assert.Equal(FixedNow.AddMinutes(10), response.SecretExpiresAt);

        var credential = await dbContext.MfaCredentials.SingleAsync();
        Assert.Equal(user.UserId, credential.UserId);
        Assert.Equal(MfaCredentialStatus.Pending, credential.Status);
        Assert.Equal([0xAA, 0xBB], credential.EncryptedSecret);
        Assert.Null(credential.ConfirmedAt);
    }

    [Fact]
    public async Task Handle_ExistingEnrollment_ReplacesRowRatherThanAddingASecondOne()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var user = SeedUser(dbContext, "user@example.com");
        dbContext.MfaCredentials.Add(MfaCredential.BeginEnrollment(user.UserId, [0x01], FixedNow.AddMinutes(-5), TimeSpan.FromMinutes(10)));
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = CreateHandler(dbContext);

        await handler.Handle(new EnrollMfaCommand(user.UserId), CancellationToken.None);

        var credential = await dbContext.MfaCredentials.SingleAsync();
        Assert.Equal([0xAA, 0xBB], credential.EncryptedSecret);
        Assert.Equal(FixedNow, credential.EnrolledAt);
    }

    private static User SeedUser(IdentityDbContext dbContext, string email)
    {
        var user = User.RegisterNative(email, "hash", "Test User", FixedNow);
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        return user;
    }

    private static EnrollMfaCommandHandler CreateHandler(IIdentityDbContext dbContext)
    {
        var totpProvisioningService = Substitute.For<ITotpProvisioningService>();
        totpProvisioningService.GenerateEnrollment(Arg.Any<string>())
            .Returns(new TotpEnrollment("BASE32SECRET", "otpauth://totp/fake-uri"));

        var mfaSecretCipher = Substitute.For<IMfaSecretCipher>();
        mfaSecretCipher.Encrypt(Arg.Any<string>()).Returns([0xAA, 0xBB]);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new EnrollMfaCommandHandler(dbContext, totpProvisioningService, mfaSecretCipher, dateTimeProvider);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
