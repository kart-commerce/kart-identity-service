using System.Security.Cryptography;
using Kart.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class RsaJwtKeyProviderTests
{
    [Fact]
    public void GetPublicSigningKeys_ExposesConfiguredKidAndRs256PublicKeyOnly()
    {
        using var rsa = RSA.Create(2048);
        var privateKeyPem = rsa.ExportPkcs8PrivateKeyPem();
        var expectedParameters = rsa.ExportParameters(includePrivateParameters: false);

        var options = Options.Create(new JwtSigningKeyOptions
        {
            Kid = "unit-test-kid",
            PrivateKeyPem = privateKeyPem
        });
        using var provider = new RsaJwtKeyProvider(options);

        var keys = provider.GetPublicSigningKeys();

        var key = Assert.Single(keys);
        Assert.Equal("RSA", key.Kty);
        Assert.Equal("sig", key.Use);
        Assert.Equal("RS256", key.Alg);
        Assert.Equal("unit-test-kid", key.Kid);
        Assert.Equal(expectedParameters.Modulus, Base64UrlDecode(key.N));
        Assert.Equal(expectedParameters.Exponent, Base64UrlDecode(key.E));
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
        return Convert.FromBase64String(padded);
    }
}
