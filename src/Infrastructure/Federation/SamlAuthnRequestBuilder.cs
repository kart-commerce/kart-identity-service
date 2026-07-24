using System.IO.Compression;
using System.Text;
using System.Web;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Application.Common.Models;

namespace Kart.Identity.Infrastructure.Federation;

/// <summary>
/// Builds a minimal SAML 2.0 AuthnRequest and encodes it per the HTTP-Redirect
/// binding (deflate, base64, URL-encode) — the standard SP-initiated redirect
/// shape api-contract.yaml GET /auth/sso/enterprise/{idpAlias}/login returns.
/// </summary>
public sealed class SamlAuthnRequestBuilder : ISamlAuthnRequestBuilder
{
    public string BuildRedirectUrl(EnterpriseIdpDescriptor idp, DateTimeOffset now)
    {
        var requestId = $"_{Guid.NewGuid():N}";
        var xml =
            $"""
             <samlp:AuthnRequest xmlns:samlp="urn:oasis:names:tc:SAML:2.0:protocol" xmlns:saml="urn:oasis:names:tc:SAML:2.0:assertion" ID="{requestId}" Version="2.0" IssueInstant="{now:yyyy-MM-ddTHH:mm:ss.fffZ}" Destination="{idp.SsoUrl}" AssertionConsumerServiceURL="{idp.AssertionConsumerServiceUrl}" ProtocolBinding="urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"><saml:Issuer>{idp.SpEntityId}</saml:Issuer></samlp:AuthnRequest>
             """;

        var encoded = DeflateAndBase64Encode(xml);
        var separator = idp.SsoUrl.Contains('?') ? '&' : '?';
        return $"{idp.SsoUrl}{separator}SAMLRequest={HttpUtility.UrlEncode(encoded)}";
    }

    private static string DeflateAndBase64Encode(string xml)
    {
        using var output = new MemoryStream();
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            deflate.Write(bytes, 0, bytes.Length);
        }

        return Convert.ToBase64String(output.ToArray());
    }
}
