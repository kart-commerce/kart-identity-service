using System.Data.Common;
using System.Security.Cryptography;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Kart.Identity.ContractTests;

/// <summary>
/// Same DB/Redis test-double swap as IntegrationTests/IdentityApiFactory — every
/// endpoint past GetJwks (JwksApiFactory) needs a database, and /auth/login also
/// needs the login-throttle/MFA-challenge services.
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>
{
    private readonly DbConnection _connection = new SqliteConnection("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        using var rsa = RSA.Create(2048);
        var testPrivateKeyPem = rsa.ExportPkcs8PrivateKeyPem();

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey:Kid"] = "contract-test-kid",
                ["Jwt:SigningKey:PrivateKeyPem"] = testPrivateKeyPem,
                ["Mfa:Encryption:KeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();
            _connection.Open();
            services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<ILoginAttemptThrottle>();
            services.RemoveAll<IMfaChallengeStore>();
            services.AddSingleton<ILoginAttemptThrottle, InMemoryLoginAttemptThrottle>();
            services.AddSingleton<IMfaChallengeStore, InMemoryMfaChallengeStore>();

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
