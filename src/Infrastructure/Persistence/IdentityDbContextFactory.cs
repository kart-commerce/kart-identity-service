using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Kart.Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time-only factory `dotnet ef migrations add`/`database update` use to
/// build <see cref="IdentityDbContext"/> without spinning up the full Api host
/// (and its own required configuration, e.g. the JWT signing key). Never used at
/// runtime — the app's own DI registration (Infrastructure/DependencyInjection.cs)
/// takes over there.
/// </summary>
public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=kart_identity;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
