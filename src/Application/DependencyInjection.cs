using System.Reflection;
using FluentValidation;
using Kart.Identity.Application.Common.Behaviours;
using Microsoft.Extensions.DependencyInjection;

namespace Kart.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
