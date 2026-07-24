using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Kart.Identity.ContractTests;

/// <summary>
/// appsettings.json deliberately omits Jwt:SigningKey:PrivateKeyPem (a secret,
/// supplied via env var/K8s Secret in real environments — see appsettings.json).
/// Tests supply their own ephemeral key so <c>ValidateOnStart</c> doesn't fail
/// host startup.
/// </summary>
public sealed class JwksApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        using var rsa = RSA.Create(2048);
        var testPrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey:Kid"] = "contract-test-kid",
                ["Jwt:SigningKey:PrivateKeyPem"] = testPrivateKeyPem
            });
        });
    }
}
