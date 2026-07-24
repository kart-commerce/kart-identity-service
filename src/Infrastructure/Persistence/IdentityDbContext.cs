using Kart.Identity.Application.Common.Interfaces;
using Kart.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Infrastructure.Persistence;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IIdentityDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();
    public DbSet<MfaCredential> MfaCredentials => Set<MfaCredential>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // No-ops on any non-Npgsql provider (Sqlite/InMemory, used in tests) — only
        // the Npgsql migrations generator interprets these annotations.
        modelBuilder.HasPostgresExtension("citext");
        modelBuilder.HasPostgresExtension("pgcrypto");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(IdentityDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
