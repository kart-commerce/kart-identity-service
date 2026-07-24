using Kart.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kart.Identity.Application.Common.Interfaces;

/// <summary>
/// Owned by Application, implemented by Infrastructure's EF Core DbContext
/// (dependency inversion, coding-standards.md) — keeps vertical-slice handlers
/// testable against any provider (InMemory/Sqlite in tests, Npgsql in production)
/// without depending on a concrete EF Core provider package.
/// </summary>
public interface IIdentityDbContext
{
    DbSet<User> Users { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Session> Sessions { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<OutboxEvent> OutboxEvents { get; }
    DbSet<MfaCredential> MfaCredentials { get; }
    DbSet<ServicePrincipal> ServicePrincipals { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
