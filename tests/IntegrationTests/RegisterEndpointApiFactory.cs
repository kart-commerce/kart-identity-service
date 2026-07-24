using System.Data.Common;
using System.Security.Cryptography;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kart.Identity.IntegrationTests;

/// <summary>
/// Swaps the production Npgsql-backed <see cref="IdentityDbContext"/> registration
/// for an in-memory Sqlite one, so the full register-endpoint request pipeline
/// (routing, MediatR validation behaviour, DI wiring, JSON serialization, exception
/// handling) can be exercised over real HTTP without a Postgres dependency. Uses
/// <c>EnsureCreated</c> (not the Npgsql-flavored migrations) since the schema is
/// built directly from the current EF model against whichever provider is active.
/// </summary>
public sealed class RegisterEndpointApiFactory : WebApplicationFactory<Program>
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
                ["Jwt:SigningKey:Kid"] = "integration-test-kid",
                ["Jwt:SigningKey:PrivateKeyPem"] = testPrivateKeyPem
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<IdentityDbContext>>();

            _connection.Open();
            services.AddDbContext<IdentityDbContext>(options => options.UseSqlite(_connection));

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
