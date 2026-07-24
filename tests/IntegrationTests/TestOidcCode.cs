using System.Text;
using System.Text.Json;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Tests can't control a real IdP's authorization-code issuance, so the fake
/// authorization "code" these tests send to the OIDC callback endpoints is
/// itself just a JSON-encoded description of the identity <see cref="FakeOidcTokenEndpointHandler"/>
/// should mint an id_token for — lets the real token-exchange/signature-validation
/// path in <c>OidcTokenExchangeClient</c> run end to end instead of being mocked.
/// </summary>
public static class TestOidcCode
{
    public sealed record Claims(string Subject, string? Email, IReadOnlyList<string> Groups);

    public static string Encode(string subject, string? email = null, IReadOnlyList<string>? groups = null) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Claims(subject, email, groups ?? []))));

    public static Claims Decode(string code) =>
        JsonSerializer.Deserialize<Claims>(Encoding.UTF8.GetString(Convert.FromBase64String(code)))!;
}
