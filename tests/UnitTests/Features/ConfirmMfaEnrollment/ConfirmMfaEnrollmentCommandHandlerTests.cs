using Kart.Identity.Application.Common.Exceptions;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Features.ConfirmMfaEnrollment;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Kart.Identity.UnitTests.Features.ConfirmMfaEnrollment;

public class ConfirmMfaEnrollmentCommandHandlerTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task Handle_ValidCodeAgainstPendingCredential_ActivatesIt()
    {
        await using var dbContext = CreateInMemoryDbContext();
        SeedPendingCredential(dbContext, expiresAt: FixedNow.AddMinutes(5));

        var handler = CreateHandler(dbContext, codeIsValid: true);

        await handler.Handle(new ConfirmMfaEnrollmentCommand(UserId, "123456"), CancellationToken.None);

        var credential = await dbContext.MfaCredentials.SingleAsync();
        Assert.Equal(MfaCredentialStatus.Active, credential.Status);
        Assert.Equal(FixedNow, credential.ConfirmedAt);
        Assert.Null(credential.PendingExpiresAt);
    }

    [Fact]
    public async Task Handle_WrongCode_ThrowsAndLeavesCredentialPending()
    {
        await using var dbContext = CreateInMemoryDbContext();
        SeedPendingCredential(dbContext, expiresAt: FixedNow.AddMinutes(5));

        var handler = CreateHandler(dbContext, codeIsValid: false);

        await Assert.ThrowsAsync<InvalidOrExpiredMfaCodeException>(
            () => handler.Handle(new ConfirmMfaEnrollmentCommand(UserId, "000000"), CancellationToken.None));

        var credential = await dbContext.MfaCredentials.SingleAsync();
        Assert.Equal(MfaCredentialStatus.Pending, credential.Status);
    }

    [Fact]
    public async Task Handle_ExpiredPendingWindow_ThrowsEvenWithACorrectCode()
    {
        await using var dbContext = CreateInMemoryDbContext();
        SeedPendingCredential(dbContext, expiresAt: FixedNow.AddMinutes(-1));

        var handler = CreateHandler(dbContext, codeIsValid: true);

        await Assert.ThrowsAsync<InvalidOrExpiredMfaCodeException>(
            () => handler.Handle(new ConfirmMfaEnrollmentCommand(UserId, "123456"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NoEnrollmentExists_Throws()
    {
        await using var dbContext = CreateInMemoryDbContext();

        var handler = CreateHandler(dbContext, codeIsValid: true);

        await Assert.ThrowsAsync<InvalidOrExpiredMfaCodeException>(
            () => handler.Handle(new ConfirmMfaEnrollmentCommand(UserId, "123456"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyActiveCredential_Throws()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var credential = MfaCredential.BeginEnrollment(UserId, [0x01], FixedNow.AddMinutes(-5), TimeSpan.FromMinutes(10));
        credential.Confirm(FixedNow.AddMinutes(-1));
        dbContext.MfaCredentials.Add(credential);
        await dbContext.SaveChangesAsync(CancellationToken.None);

        var handler = CreateHandler(dbContext, codeIsValid: true);

        await Assert.ThrowsAsync<InvalidOrExpiredMfaCodeException>(
            () => handler.Handle(new ConfirmMfaEnrollmentCommand(UserId, "123456"), CancellationToken.None));
    }

    private static void SeedPendingCredential(IdentityDbContext dbContext, DateTimeOffset expiresAt)
    {
        var pendingWindow = TimeSpan.FromMinutes(10);
        var credential = MfaCredential.BeginEnrollment(UserId, [0xAA, 0xBB], expiresAt.Subtract(pendingWindow), pendingWindow);
        dbContext.MfaCredentials.Add(credential);
        dbContext.SaveChanges();
    }

    private static ConfirmMfaEnrollmentCommandHandler CreateHandler(IIdentityDbContext dbContext, bool codeIsValid)
    {
        var mfaSecretCipher = Substitute.For<IMfaSecretCipher>();
        mfaSecretCipher.Decrypt(Arg.Any<byte[]>()).Returns("BASE32SECRET");

        var totpCodeValidator = Substitute.For<ITotpCodeValidator>();
        totpCodeValidator.IsCodeValid(Arg.Any<string>(), Arg.Any<string>()).Returns(codeIsValid);

        var dateTimeProvider = Substitute.For<IDateTimeProvider>();
        dateTimeProvider.UtcNow.Returns(FixedNow);

        return new ConfirmMfaEnrollmentCommandHandler(dbContext, mfaSecretCipher, totpCodeValidator, dateTimeProvider, NullLogger<ConfirmMfaEnrollmentCommandHandler>.Instance);
    }

    private static IdentityDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }
}
