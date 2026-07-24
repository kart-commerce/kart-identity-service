using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OtpNet;
using YamlDotNet.Serialization;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Verifies ConfirmMfaEnrollment (IDN-5) against contracts/api-contract.yaml's
/// POST /auth/mfa/enroll/confirm path — both that the contract still describes
/// the shape this handler implements, and that the live endpoint matches it.
/// </summary>
public class ConfirmMfaEnrollmentContractTests : IClassFixture<IdentityApiFactory>
{
    private const string ContractPath = "/auth/mfa/enroll/confirm";
    private const string RegisterRequestPath = "/v1/auth/register";
    private const string EnrollRequestPath = "/v1/auth/mfa/enroll";
    private const string ConfirmRequestPath = "/v1/auth/mfa/enroll/confirm";
    private readonly IdentityApiFactory _factory;

    public ConfirmMfaEnrollmentContractTests(IdentityApiFactory factory) => _factory = factory;

    [Fact]
    public void Contract_DefinesConfirmMfaEnrollmentPathAsBearerAuthenticated()
    {
        var contract = LoadContract();

        var paths = (Dictionary<object, object>)contract["paths"];
        Assert.True(paths.ContainsKey(ContractPath), $"api-contract.yaml no longer defines {ContractPath}");

        var postOp = (Dictionary<object, object>)((Dictionary<object, object>)paths[ContractPath])["post"];
        Assert.Equal("confirmMfaEnrollment", postOp["operationId"]);
        Assert.True(postOp.ContainsKey("security"), $"POST {ContractPath} should require bearer auth");

        var responses = (Dictionary<object, object>)postOp["responses"];
        Assert.True(responses.ContainsKey("200"));
        Assert.True(responses.ContainsKey("400"));
    }

    [Fact]
    public async Task LiveEndpoint_ValidCode_Returns200()
    {
        var client = _factory.CreateClient();
        var email = $"mfa-confirm-contract-{Guid.NewGuid():N}@example.com";
        var registerResponse = await client.PostAsJsonAsync(RegisterRequestPath, new { email, password = "SuperSecret1" });
        using var registerBody = JsonDocument.Parse(await registerResponse.Content.ReadAsStringAsync());
        var accessToken = registerBody.RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var enrollResponse = await client.PostAsync(EnrollRequestPath, content: null);
        using var enrollBody = JsonDocument.Parse(await enrollResponse.Content.ReadAsStringAsync());
        var provisioningUri = enrollBody.RootElement.GetProperty("provisioningUri").GetString()!;
        var base32Secret = ExtractSecret(provisioningUri);
        var totpCode = new Totp(Base32Encoding.ToBytes(base32Secret)).ComputeTotp();

        var response = await client.PostAsJsonAsync(ConfirmRequestPath, new { totpCode });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LiveEndpoint_NoBearerToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(ConfirmRequestPath, new { totpCode = "123456" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ExtractSecret(string provisioningUri)
    {
        var query = new Uri(provisioningUri).Query.TrimStart('?');
        var secretParam = query.Split('&').Single(p => p.StartsWith("secret=", StringComparison.Ordinal));
        return secretParam["secret=".Length..];
    }

    private static Dictionary<object, object> LoadContract()
    {
        var yamlPath = Path.Combine(AppContext.BaseDirectory, "api-contract.yaml");
        var yaml = File.ReadAllText(yamlPath);
        var deserializer = new DeserializerBuilder().Build();
        return deserializer.Deserialize<Dictionary<object, object>>(yaml);
    }
}
