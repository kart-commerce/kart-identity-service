using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
    public const string TestIdpAlias = "test-idp";
    public const string TestIdpSpEntityId = "kart-identity-service-test";
    public const string TestIdpSsoUrl = "https://idp.example.com/sso";
    public const string TestIdpAcsUrl = "https://identity.example.com/acs";

    private readonly DbConnection _connection = new SqliteConnection("DataSource=:memory:");

    /// <summary>
    /// Same certificate configured as `test-idp`'s signing cert below — tests sign
    /// their own fake SAML responses with this (private key included) so the ACS
    /// endpoint's real signature verification has something genuine to check.
    /// </summary>
    public X509Certificate2 TestIdpCertificate { get; }

    public IdentityApiFactory()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={TestIdpAlias}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeralCert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        TestIdpCertificate = new X509Certificate2(ephemeralCert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
    }

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
                ["Mfa:Encryption:KeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                [$"EnterpriseIdps:{TestIdpAlias}:SsoUrl"] = TestIdpSsoUrl,
                [$"EnterpriseIdps:{TestIdpAlias}:SpEntityId"] = TestIdpSpEntityId,
                [$"EnterpriseIdps:{TestIdpAlias}:AssertionConsumerServiceUrl"] = TestIdpAcsUrl,
                [$"EnterpriseIdps:{TestIdpAlias}:SigningCertificatePem"] = TestIdpCertificate.ExportCertificatePem()
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
            services.RemoveAll<ITokenRevocationStore>();
            services.RemoveAll<ISamlAssertionReplayStore>();
            services.AddSingleton<ILoginAttemptThrottle, InMemoryLoginAttemptThrottle>();
            services.AddSingleton<IMfaChallengeStore, InMemoryMfaChallengeStore>();
            services.AddSingleton<ITokenRevocationStore, InMemoryTokenRevocationStore>();
            services.AddSingleton<ISamlAssertionReplayStore, InMemorySamlAssertionReplayStore>();

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
