using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `service_principals` — `client_id` (not a generated Guid) is the table's own primary key.</summary>
public sealed class ServicePrincipalConfiguration : IEntityTypeConfiguration<ServicePrincipal>
{
    public void Configure(EntityTypeBuilder<ServicePrincipal> builder)
    {
        builder.ToTable("service_principals", t =>
        {
            t.HasCheckConstraint("ck_service_principals_role", "role IN ('admin', 'partner_api')");
            t.HasCheckConstraint("ck_service_principals_status", "status IN ('active', 'revoked')");
        });

        builder.HasKey(sp => sp.ClientId);
        builder.Property(sp => sp.ClientId)
            .HasColumnName("client_id")
            .ValueGeneratedNever();

        builder.Property(sp => sp.ClientSecretHash).HasColumnName("client_secret_hash").IsRequired();

        builder.Property(sp => sp.Role)
            .HasColumnName("role")
            .HasConversion(EnumDbValueConverters.PlatformRole)
            .IsRequired();

        builder.Property(sp => sp.Status)
            .HasColumnName("status")
            .HasConversion(EnumDbValueConverters.ServicePrincipalStatus)
            .IsRequired();

        builder.Property(sp => sp.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(sp => sp.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(sp => sp.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(sp => sp.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
