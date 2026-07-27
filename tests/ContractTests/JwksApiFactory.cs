using System.Security.Cryptography;
using Kart.Identity.Infrastructure.Messaging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        // No real RabbitMQ in this test environment — the JWKS endpoint doesn't touch
        // messaging at all (same reasoning as IdentityApiFactory's own removal).
        builder.ConfigureServices(services =>
        {
            RemoveHostedService<OutboxRelayHostedService>(services);
            RemoveHostedService<UserDataErasedConsumerHostedService>(services);
        });
    }

    private static void RemoveHostedService<T>(IServiceCollection services)
        where T : class, IHostedService
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(T));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }
}
