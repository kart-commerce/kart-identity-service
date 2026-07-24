using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using Kart.Identity.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace Kart.Identity.UnitTests.Infrastructure.Security;

public class JwtAccessTokenGeneratorTests
{
    [Fact]
    public void Generate_ProducesRs256TokenCarryingUserIdRolesAndScopes()
    {
        using var rsa = RSA.Create(2048);
        var options = Options.Create(new JwtSigningKeyOptions
        {
            Kid = "unit-test-kid",
            PrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem()
        });
        using var generator = new JwtAccessTokenGenerator(options);
        var userId = Guid.NewGuid();

        var accessToken = generator.Generate(userId, ["customer"], []);

        Assert.Equal(900, accessToken.ExpiresInSeconds);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken.Token);
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Equal("unit-test-kid", jwt.Header.Kid);
        Assert.Equal(userId.ToString(), jwt.Subject);
        Assert.Contains(jwt.Claims, c => c.Type == "roles" && c.Value == "customer");
    }
}
