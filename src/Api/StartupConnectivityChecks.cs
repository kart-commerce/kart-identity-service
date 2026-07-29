using Kart.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Kart.Identity.Api;

/// <summary>Verifies every infra dependency is reachable right after boot, one Connecting/connected
/// log pair per dependency, so a misconfigured or unreachable Postgres/Redis/RabbitMQ shows up
/// immediately in the startup log instead of surfacing later as the first request's failure.</summary>
public static class StartupConnectivityChecks
{
    public static async Task RunAsync(WebApplication app)
    {
        // WebApplicationFactory-based tests (Contract/Integration) run this same Program.cs but
        // swap Postgres for SQLite and remove the Redis/RabbitMQ registrations entirely - real
        // connectivity is neither available nor meaningful there, so those factories mark
        // themselves "Testing" and this step is a deliberate no-op for them.
        if (app.Environment.IsEnvironment("Testing"))
        {
            return;
        }

        var logger = app.Logger;

        await CheckAsync(logger, "PostgresDB", async () =>
        {
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await dbContext.Database.CanConnectAsync();
        });

        await CheckAsync(logger, "Redis", () =>
        {
            app.Services.GetRequiredService<IConnectionMultiplexer>();
            return Task.CompletedTask;
        });

        await CheckAsync(logger, "RabbitMQ", () =>
        {
            var connectionFactory = app.Services.GetRequiredService<IConnectionFactory>();
            using var connection = connectionFactory.CreateConnection();
            return Task.CompletedTask;
        });
    }

    private static async Task CheckAsync(ILogger logger, string dependency, Func<Task> connect)
    {
        logger.LogInformation("Connecting Identity {Dependency} ...", dependency);
        try
        {
            await connect();
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Failed to connect to Identity {Dependency}", dependency);
            throw;
        }

        logger.LogInformation("{Dependency} connected", dependency);
    }
}
