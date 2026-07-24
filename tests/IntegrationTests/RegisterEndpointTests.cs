using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Kart.Identity.IntegrationTests;

/// <summary>Exercises api-contract.yaml POST /v1/auth/register end to end over real HTTP.</summary>
public class RegisterEndpointTests : IClassFixture<RegisterEndpointApiFactory>
{
    private const string RegisterPath = "/v1/auth/register";
    private readonly RegisterEndpointApiFactory _factory;

    public RegisterEndpointTests(RegisterEndpointApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_NewAccount_Returns201WithTokenPair()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync(RegisterPath, new
        {
            email,
            password = "SuperSecret1",
            displayName = "Test User"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("refreshToken").GetString()));
        Assert.Equal("Bearer", root.GetProperty("tokenType").GetString());
        Assert.Equal(900, root.GetProperty("expiresIn").GetInt32());
        Assert.Equal("customer", root.GetProperty("roles")[0].GetString());
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Problem()
    {
        var client = _factory.CreateClient();
        var email = UniqueEmail();
        var payload = new { email, password = "SuperSecret1", displayName = "Test User" };

        var first = await client.PostAsJsonAsync(RegisterPath, payload);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync(RegisterPath, payload);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var body = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.Equal("email_already_registered", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_PasswordTooShort_Returns400Problem()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(RegisterPath, new
        {
            email = UniqueEmail(),
            password = "short"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_error", body.RootElement.GetProperty("code").GetString());
    }

    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@example.com";
}
