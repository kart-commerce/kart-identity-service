using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.Enums;
using Kart.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kart.Identity.Infrastructure.Seeding;

/// <summary>
/// Dev/local-only bootstrap for `service_principals` — the out-of-band provisioning gap
/// ServicePrincipal.Provision's own doc comment flags (tickets.md: nothing else in this service
/// ever writes to that table, so without this, no client_id can ever pass POST /v1/auth/token's
/// client-credentials grant). Absent/empty `ServicePrincipalSeeds` config is a no-op, so this is
/// safe to run unconditionally on every startup; only kart-devops' docker-compose globalconfig
/// and kart-internals' bare-metal globalconfig populate it, each with the one client_id its own
/// environment's kart-admin-service actually authenticates as (IdentityClientCredentialsOptions
/// on that side). Skips any ClientId that already has a row, so re-running is idempotent.
/// </summary>
public static class ServicePrincipalSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken)
    {
        var entries = configuration.GetSection("ServicePrincipalSeeds").Get<List<SeedEntry>>() ?? [];
        if (entries.Count == 0)
        {
            return;
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IIdentityDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ServicePrincipalSeeder");

        foreach (var entry in entries)
        {
            var clientId = ServicePrincipalClientId.From(entry.ClientId);
            var exists = await dbContext.ServicePrincipals.AnyAsync(sp => sp.ClientId == clientId, cancellationToken);
            if (exists)
            {
                continue;
            }

            if (!Enum.TryParse<PlatformRole>(entry.Role, ignoreCase: true, out var role))
            {
                logger.LogWarning("ServicePrincipalSeeds entry for {ClientId} has an unrecognized Role {Role} — skipped.", entry.ClientId, entry.Role);
                continue;
            }

            var principal = ServicePrincipal.Provision(
                clientId,
                passwordHasher.Hash(entry.ClientSecret),
                role,
                DateTimeOffset.UtcNow,
                createdBy: "startup-seed");

            dbContext.ServicePrincipals.Add(principal);
            logger.LogInformation("Seeded service principal {ClientId} with role {Role}.", entry.ClientId, role);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedEntry(string ClientId, string ClientSecret, string Role);
}
