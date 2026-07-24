using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `users` — the `UserIdentity` aggregate root.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users", t => t.HasCheckConstraint(
            "ck_users_account_origin", "account_origin IN ('native', 'social', 'enterprise')"));

        builder.HasKey(u => u.UserId);
        builder.Property(u => u.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasColumnType("citext");
        builder.HasIndex(u => u.Email)
            .IsUnique()
            .HasDatabaseName("uq_users_email")
            .HasFilter("email IS NOT NULL");

        builder.Property(u => u.PasswordHash).HasColumnName("password_hash");

        builder.Property(u => u.DisplayName)
            .HasColumnName("display_name")
            .IsRequired();

        builder.Property(u => u.AccountOrigin)
            .HasColumnName("account_origin")
            .HasConversion(EnumDbValueConverters.AccountOrigin)
            .IsRequired();

        builder.Property(u => u.LockedAt).HasColumnName("locked_at");
        builder.Property(u => u.LockedBy).HasColumnName("locked_by");

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
