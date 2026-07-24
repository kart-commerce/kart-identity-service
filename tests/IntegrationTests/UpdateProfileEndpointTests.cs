using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml PATCH /v1/auth/profile end to end over real HTTP.</summary>
public class UpdateProfileEndpointTests : IClassFixture<IdentityApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private const string ProfilePath = "/v1/auth/profile";
    private readonly IdentityApiFactory _factory;

    public UpdateProfileEndpointTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public async Task UpdateProfile_ValidBody_Returns200AndPersistsAndPublishesEvent()
    {
        var client = _factory.CreateClient();
        var (accessToken, _) = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var newEmail = $"updated-{Guid.NewGuid():N}@example.com";

        var response = await client.PatchAsJsonAsync(ProfilePath, new { email = newEmail, displayName = "Updated Name" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(newEmail, body.RootElement.GetProperty("email").GetString());
        Assert.Equal("Updated Name", body.RootElement.GetProperty("displayName").GetString());

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await dbContext.Users.SingleAsync(u => u.Email == newEmail);
        Assert.Equal("Updated Name", user.DisplayName);
        var outboxEvent = await dbContext.OutboxEvents.SingleAsync(e => e.AggregateId == user.UserId && e.EventType == "UserAccountUpdated");
        Assert.Contains(newEmail, outboxEvent.Payload);
    }

    [Fact]
    public async Task UpdateProfile_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PatchAsJsonAsync(ProfilePath, new { displayName = "New Name" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_EmptyBody_Returns400()
    {
        var client = _factory.CreateClient();
        var (accessToken, _) = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PatchAsJsonAsync(ProfilePath, new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_EmailAlreadyRegisteredToAnotherAccount_Returns409()
    {
        var client = _factory.CreateClient();
        var (_, otherEmail) = await RegisterAsync(client);
        var (accessToken, _) = await RegisterAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.PatchAsJsonAsync(ProfilePath, new { email = otherEmail });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private async Task<(string AccessToken, string Email)> RegisterAsync(HttpClient client)
    {
        var email = $"profile-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync(RegisterPath, new { email, password = "SuperSecret1", displayName = "Test User" });
        response.EnsureSuccessStatusCode();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (body.RootElement.GetProperty("accessToken").GetString()!, email);
    }
}
