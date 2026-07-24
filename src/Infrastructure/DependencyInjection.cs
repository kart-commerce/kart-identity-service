using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Infrastructure.Federation;
using Kart.Identity.Infrastructure.Persistence;
using Kart.Identity.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

        return services;
    }
}
