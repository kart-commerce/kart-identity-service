using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
