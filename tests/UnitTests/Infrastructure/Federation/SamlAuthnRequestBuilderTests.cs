using System.IO.Compression;
using System.Web;
using Kart.Identity.Application.Common.Models;
using Kart.Identity.Infrastructure.Federation;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Federation;

public class SamlAuthnRequestBuilderTests
{
    private static readonly DateTimeOffset FixedNow = new(2026, 7, 24, 12, 0, 0, TimeSpan.Zero);
    private readonly SamlAuthnRequestBuilder _builder = new();

    [Fact]
    public void BuildRedirectUrl_ProducesUrlTargetingIdpSsoEndpointWithInflatableRequest()
    {
        var idp = new EnterpriseIdpDescriptor(
            "okta-acme", "https://idp.example.com/sso", "kart-identity-service", "https://identity.example.com/acs", "unused-in-this-test");

        var redirectUrl = _builder.BuildRedirectUrl(idp, FixedNow);

        Assert.StartsWith("https://idp.example.com/sso?SAMLRequest=", redirectUrl);

        var uri = new Uri(redirectUrl);
        var query = HttpUtility.ParseQueryString(uri.Query);
        var encoded = query["SAMLRequest"]!;
        var xml = Inflate(encoded);

        Assert.Contains("<samlp:AuthnRequest", xml);
        Assert.Contains("Destination=\"https://idp.example.com/sso\"", xml);
        Assert.Contains("AssertionConsumerServiceURL=\"https://identity.example.com/acs\"", xml);
        Assert.Contains("<saml:Issuer>kart-identity-service</saml:Issuer>", xml);
    }

    private static string Inflate(string base64)
    {
        var compressed = Convert.FromBase64String(base64);
        using var input = new MemoryStream(compressed);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        return System.Text.Encoding.UTF8.GetString(output.ToArray());
    }
}
