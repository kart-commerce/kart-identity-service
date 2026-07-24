using System.Net;
using System.Net.Http.Json;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies ConfirmPasswordReset (IDN-13) against contracts/api-contract.yaml's
/// POST /auth/password/reset-confirm path.
/// </summary>
public class ConfirmPasswordResetContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/password/reset-confirm";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string ConfirmRequestPath = "/v1/auth/password/reset-confirm";
    private readonly IdentityApiFactory _factory;

    public ConfirmPasswordResetContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesResetConfirmPathWith200And400()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("confirmPasswordReset", postOp["operationId"]);

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("400"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidToken_Returns200()
    {
        var client = _factory.CreateClient();
        var email = $"reset-confirm-contract-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterRequestPath, new { email, password = "OldPassword1" });
        registerResponse.EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var tokenHasher = scope.ServiceProvider.GetRequiredService<ITokenHasher>();
        var user = await dbContext.Users.SingleAsync(u => u.Email == email);
        var rawResetToken = $"raw-reset-token-{Guid.NewGuid():N}";
        dbContext.PasswordResetTokens.Add(PasswordResetToken.Issue(user.UserId, tokenHasher.Hash(rawResetToken), DateTimeOffset.UtcNow));
        await dbContext.SaveChangesAsync();

        var response = await client.PostAsJsonAsync(ConfirmRequestPath, new { resetToken = rawResetToken, newPassword = "NewPassword1" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LiveEndpoint_UnknownToken_Returns400()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(ConfirmRequestPath, new { resetToken = "does-not-exist", newPassword = "NewPassword1" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
