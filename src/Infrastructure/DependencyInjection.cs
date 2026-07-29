using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Infrastructure.Federation;
using Kart.Identity.Infrastructure.Messaging;
using Kart.Identity.Infrastructure.Persistence;
using Kart.Identity.Infrastructure.Security;
using Kart.Shared.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

namespace Kart.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<JwtSigningKeyOptions>()
            .Bind(configuration.GetSection(JwtSigningKeyOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IJwtKeyProvider, RsaJwtKeyProvider>();

        // Identity is both issuer and, for its own bearer-protected endpoints
        // (e.g. POST /auth/mfa/enroll), validator of its own RS256 tokens —
        // validated against the same public key it publishes via JWKS.
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IJwtKeyProvider>((options, keyProvider) =>
            {
                var resolver = new JwksIssuerSigningKeyResolver(keyProvider);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = resolver.Resolve,
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true
                };
            });
        services.AddAuthorization();
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IOpaqueTokenGenerator, SecureOpaqueTokenGenerator>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ITotpProvisioningService, TotpProvisioningService>();
        services.AddSingleton<ITotpCodeValidator, TotpCodeValidator>();

        services
            .AddOptions<MfaEncryptionOptions>()
            .Bind(configuration.GetSection(MfaEncryptionOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IMfaSecretCipher, AesMfaSecretCipher>();

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("IdentityDb")));
        services.AddScoped<IIdentityDbContext>(sp => sp.GetRequiredService<IdentityDbContext>());

        // design-decisions.md, "Shared State-Store Technology for Ephemeral
        // Security State" — single shared Redis deployment, namespaced per use
        // case. Lazily connected on first resolve, not at registration time.
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(configuration.GetConnectionString("Redis")!));
        services.AddScoped<ILoginAttemptThrottle, RedisLoginAttemptThrottle>();
        services.AddScoped<IMfaChallengeStore, RedisMfaChallengeStore>();
        services.AddScoped<ITokenRevocationStore, RedisTokenRevocationStore>();
        services.AddScoped<ISamlAssertionReplayStore, RedisSamlAssertionReplayStore>();

        // No enterprise IdP is named as already-integrated anywhere in the design
        // docs (Okta/Azure AD/Google Workspace are the BRD's illustrative examples
        // only) — this section is empty by default in every environment until an
        // operator configures a real one.
        services.Configure<Dictionary<string, EnterpriseIdpConfig>>(configuration.GetSection("EnterpriseIdps"));
        services.AddSingleton<IEnterpriseIdpDirectory, ConfigEnterpriseIdpDirectory>();
        services.AddSingleton<ISamlAuthnRequestBuilder, SamlAuthnRequestBuilder>();
        services.AddSingleton<ISamlAssertionValidator, SamlAssertionValidator>();

        // Customer social-login providers (IDN-17/IDN-18) — same "empty by
        // default, operator-configured" treatment as EnterpriseIdps above.
        services.Configure<Dictionary<string, SocialIdpConfig>>(configuration.GetSection("SocialIdps"));
        services.AddSingleton<ISocialIdpDirectory, ConfigSocialIdpDirectory>();

        // Shared OIDC relying-party building blocks for enterprise OIDC federation
        // (IDN-16) and social login (IDN-17/IDN-18). The named HttpClient carries no
        // per-provider resilience of its own — design-decisions.md's per-IdP circuit
        // breaker/bulkhead/timeout is applied inside OidcTokenExchangeClient, keyed on
        // each provider's own ProviderKey, not on this shared transport client.
        services.AddHttpClient("oidc-token-exchange");
        services.AddSingleton<IOidcAuthorizationRequestBuilder, OidcAuthorizationRequestBuilder>();
        services.AddSingleton<IOidcTokenExchangeClient, OidcTokenExchangeClient>();

        // contracts/message-bus-manifest.json is the single source of truth for this
        // service's entire RabbitMQ topology — every exchange, queue, binding, dead-letter
        // and retry-tier name. Nothing messaging-related is hardcoded in C#: the manifest-load/
        // topology-declare/connection-factory mechanics are Kart.Shared.Messaging (identical
        // across every Kart service); only this service's own RabbitMqOptions shape/validation
        // and its own publisher/consumer business logic stay local.
        services
            .AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .Validate(
                o => string.IsNullOrEmpty(o.UserName) == string.IsNullOrEmpty(o.Password),
                "RabbitMq:UserName and RabbitMq:Password must either both be set or both be left unset.")
            .ValidateOnStart();
        services.AddKartMessageBusManifest(sp => sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value.ManifestPath);
        services.AddKartRabbitMqConnectionFactory(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            return new RabbitMqConnectionSettings(options.HostName, UserName: options.UserName, Password: options.Password);
        });
        services.AddKartRabbitMqTopologyStartup();
        services.AddHostedService<OutboxRelayHostedService>();
        services.AddHostedService<UserDataErasedConsumerHostedService>();

        return services;
    }
}
