using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `idp_group_role_mappings`.</summary>
public sealed class IdpGroupRoleMappingConfiguration : IEntityTypeConfiguration<IdpGroupRoleMapping>
{
    public void Configure(EntityTypeBuilder<IdpGroupRoleMapping> builder)
    {
        builder.ToTable("idp_group_role_mappings", t => t.HasCheckConstraint(
            "ck_idp_group_role_mappings_role", "role IN ('support_agent', 'admin')"));

        builder.HasKey(m => m.MappingId);
        builder.Property(m => m.MappingId)
            .HasColumnName("mapping_id")
            .ValueGeneratedNever();

        builder.Property(m => m.IdpAlias).HasColumnName("idp_alias").IsRequired();
        builder.Property(m => m.ExternalGroupClaim).HasColumnName("external_group_claim").IsRequired();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion(EnumDbValueConverters.PlatformRole)
            .IsRequired();

        builder.HasIndex(m => new { m.IdpAlias, m.ExternalGroupClaim })
            .IsUnique()
            .HasDatabaseName("uq_idp_group_role_mappings");

        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
