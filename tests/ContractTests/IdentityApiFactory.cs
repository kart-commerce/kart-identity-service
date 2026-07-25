using System.Data.Common;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Infrastructure.Messaging;
using Kart.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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

    public const string TestOidcIdpAlias = "test-oidc-idp";
    public const string TestOidcIssuer = "https://oidc-idp.example.com";
    public const string TestOidcClientId = "kart-identity-service-oidc-client";
    public const string TestOidcAuthorizationEndpoint = "https://oidc-idp.example.com/authorize";
    public const string TestOidcTokenEndpoint = "https://oidc-idp.example.com/token";
    public const string TestOidcRedirectUri = "https://identity.example.com/oidc/callback";

    public const string TestSocialProvider = "test-social";
    public const string TestSocialIssuer = "https://social-idp.example.com";
    public const string TestSocialClientId = "kart-identity-service-social-client";
    public const string TestSocialAuthorizationEndpoint = "https://social-idp.example.com/authorize";
    public const string TestSocialTokenEndpoint = "https://social-idp.example.com/token";
    public const string TestSocialRedirectUri = "https://identity.example.com/social/callback";

    private readonly DbConnection _connection = new SqliteConnection("DataSource=:memory:");

    /// <summary>
    /// Same certificate configured as `test-idp`'s signing cert below — tests sign
    /// their own fake SAML responses with this (private key included) so the ACS
    /// endpoint's real signature verification has something genuine to check.
    /// </summary>
    public X509Certificate2 TestIdpCertificate { get; }

    /// <summary>Signs id_tokens <see cref="FakeOidcTokenEndpointHandler"/> mints for `test-oidc-idp`.</summary>
    public X509Certificate2 TestOidcIdpCertificate { get; }

    /// <summary>Signs id_tokens <see cref="FakeOidcTokenEndpointHandler"/> mints for `test-social`.</summary>
    public X509Certificate2 TestSocialIdpCertificate { get; }

    public IdentityApiFactory()
    {
        TestIdpCertificate = CreateEphemeralCertificate(TestIdpAlias);
        TestOidcIdpCertificate = CreateEphemeralCertificate(TestOidcIdpAlias);
        TestSocialIdpCertificate = CreateEphemeralCertificate(TestSocialProvider);
    }

    private static X509Certificate2 CreateEphemeralCertificate(string commonName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={commonName}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var ephemeralCert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new X509Certificate2(ephemeralCert.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
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
                [$"EnterpriseIdps:{TestIdpAlias}:SigningCertificatePem"] = TestIdpCertificate.ExportCertificatePem(),

                [$"EnterpriseIdps:{TestOidcIdpAlias}:Protocol"] = "oidc",
                [$"EnterpriseIdps:{TestOidcIdpAlias}:AuthorizationEndpoint"] = TestOidcAuthorizationEndpoint,
                [$"EnterpriseIdps:{TestOidcIdpAlias}:TokenEndpoint"] = TestOidcTokenEndpoint,
                [$"EnterpriseIdps:{TestOidcIdpAlias}:ClientId"] = TestOidcClientId,
                [$"EnterpriseIdps:{TestOidcIdpAlias}:ClientSecret"] = "test-client-secret",
                [$"EnterpriseIdps:{TestOidcIdpAlias}:RedirectUri"] = TestOidcRedirectUri,
                [$"EnterpriseIdps:{TestOidcIdpAlias}:Issuer"] = TestOidcIssuer,
                [$"EnterpriseIdps:{TestOidcIdpAlias}:SigningCertificatePem"] = TestOidcIdpCertificate.ExportCertificatePem(),

                [$"SocialIdps:{TestSocialProvider}:AuthorizationEndpoint"] = TestSocialAuthorizationEndpoint,
                [$"SocialIdps:{TestSocialProvider}:TokenEndpoint"] = TestSocialTokenEndpoint,
                [$"SocialIdps:{TestSocialProvider}:ClientId"] = TestSocialClientId,
                [$"SocialIdps:{TestSocialProvider}:ClientSecret"] = "test-client-secret",
                [$"SocialIdps:{TestSocialProvider}:RedirectUri"] = TestSocialRedirectUri,
                [$"SocialIdps:{TestSocialProvider}:Issuer"] = TestSocialIssuer,
                [$"SocialIdps:{TestSocialProvider}:SigningCertificatePem"] = TestSocialIdpCertificate.ExportCertificatePem()
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

            services.AddHttpClient("oidc-token-exchange")
                .ConfigurePrimaryHttpMessageHandler(() => new FakeOidcTokenEndpointHandler(
                    new FakeOidcProviderRegistration(TestOidcTokenEndpoint, TestOidcIssuer, TestOidcClientId, TestOidcIdpCertificate),
                    new FakeOidcProviderRegistration(TestSocialTokenEndpoint, TestSocialIssuer, TestSocialClientId, TestSocialIdpCertificate)));

            // No real RabbitMQ in this test environment — these tests assert HTTP contract
            // shape, not event publication/consumption (covered separately by the dedicated
            // messaging integration tests, which point these same hosted services at a real
            // Testcontainers RabbitMQ instance instead of removing them).
            RemoveHostedService<OutboxRelayHostedService>(services);
            RemoveHostedService<UserDataErasedConsumerHostedService>(services);

            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreated();
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
