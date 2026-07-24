using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Infrastructure.Persistence;
using Kart.Identity.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddSingleton<IAccessTokenGenerator, JwtAccessTokenGenerator>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
        services.AddSingleton<IOpaqueTokenGenerator, SecureOpaqueTokenGenerator>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

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

        return services;
    }
}
