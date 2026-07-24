using System.Net;
using System.Net.Http.Json;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Exercises api-contract.yaml POST /v1/auth/password/reset-confirm end to end
/// over real HTTP. No endpoint ever returns the raw reset token
/// (api-contract.yaml's reset-initiate is always 202 with no body, by design —
/// the token is meant for out-of-band delivery), so tests seed the row directly
/// via the DbContext, hashing a known raw value with the same ITokenHasher the
/// handler uses.
/// </summary>
public class ConfirmPasswordResetEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string LoginPath = "/v1/auth/login";
    private const string ConfirmPath = "/v1/auth/password/reset-confirm";
    private readonly IdentityApiFactory _factory;

    public ConfirmPasswordResetEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ConfirmReset_ValidToken_Returns200AndAllowsLoginWithNewPassword()
    {
        var client = _factory.CreateClient();
        var email = $"reset-confirm-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterPath, new { email, password = "OldPassword1", displayName = "Test User" });
        registerResponse.EnsureSuccessStatusCode();

        var rawResetToken = await SeedResetTokenAsync(email);

        var response = await client.PostAsJsonAsync(ConfirmPath, new { resetToken = rawResetToken, newPassword = "NewPassword1" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync(LoginPath, new { email, password = "OldPassword1" });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.PostAsJsonAsync(LoginPath, new { email, password = "NewPassword1" });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task ConfirmReset_ValidToken_RevokesOutstandingSessions()
    {
        var client = _factory.CreateClient();
        var email = $"reset-revoke-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterPath, new { email, password = "OldPassword1", displayName = "Test User" });
        registerResponse.EnsureSuccessStatusCode();

        var rawResetToken = await SeedResetTokenAsync(email);

        var response = await client.PostAsJsonAsync(ConfirmPath, new { resetToken = rawResetToken, newPassword = "NewPassword1" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userId = (await dbContext.Users.SingleAsync(u => u.Email == email)).UserId;
        var session = await dbContext.Sessions.SingleAsync(s => s.UserId == userId);
        Assert.Equal(SessionRevocationReason.PasswordReset, session.RevokedReason);
    }

    [Fact]
    public async Task ConfirmReset_UnknownToken_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(ConfirmPath, new { resetToken = "does-not-exist", newPassword = "NewPassword1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmReset_ReplayedAlreadyConsumedToken_Returns400()
    {
        var client = _factory.CreateClient();
        var email = $"reset-replay-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterPath, new { email, password = "OldPassword1", displayName = "Test User" });
        registerResponse.EnsureSuccessStatusCode();
        var rawResetToken = await SeedResetTokenAsync(email);

        var firstConfirm = await client.PostAsJsonAsync(ConfirmPath, new { resetToken = rawResetToken, newPassword = "NewPassword1" });
        Assert.Equal(HttpStatusCode.OK, firstConfirm.StatusCode);

        var replay = await client.PostAsJsonAsync(ConfirmPath, new { resetToken = rawResetToken, newPassword = "AnotherPassword1" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    [Fact]
    public async Task ConfirmReset_TooShortNewPassword_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(ConfirmPath, new { resetToken = "some-token", newPassword = "short1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<string> SeedResetTokenAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tokenHasher = scope.ServiceProvider.GetRequiredService<ITokenHasher>();

        var rawResetToken = $"raw-reset-token-{Guid.NewGuid():N}";
        var user = await dbContext.Users.SingleAsync(u => u.Email == email);
        var resetToken = PasswordResetToken.Issue(user.UserId, tokenHasher.Hash(rawResetToken), DateTimeOffset.UtcNow);
        dbContext.PasswordResetTokens.Add(resetToken);
        await dbContext.SaveChangesAsync();
        return rawResetToken;
    }
}
