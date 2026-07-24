using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `user_roles` — persisted `RoleGrant`s of the `UserIdentity` aggregate.</summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles", t => t.HasCheckConstraint(
            "ck_user_roles_role", "role IN ('customer', 'support_agent', 'admin')"));

        builder.HasKey(r => r.UserRoleId);
        builder.Property(r => r.UserRoleId)
            .HasColumnName("user_role_id")
            .ValueGeneratedNever();

        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.UserId);

        builder.Property(r => r.Role)
            .HasColumnName("role")
            .HasConversion(EnumDbValueConverters.PlatformRole)
            .IsRequired();

        builder.Property(r => r.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(r => r.GrantedBy).HasColumnName("granted_by").IsRequired();
        builder.Property(r => r.RevokedAt).HasColumnName("revoked_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasIndex(r => new { r.UserId, r.Role })
            .IsUnique()
            .HasDatabaseName("uq_user_roles_live")
            .HasFilter("revoked_at IS NULL");
    }
}
