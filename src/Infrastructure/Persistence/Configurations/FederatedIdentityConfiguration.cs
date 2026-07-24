using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `federated_identities`.</summary>
public sealed class FederatedIdentityConfiguration : IEntityTypeConfiguration<FederatedIdentity>
{
    public void Configure(EntityTypeBuilder<FederatedIdentity> builder)
    {
        builder.ToTable("federated_identities", t => t.HasCheckConstraint(
            "ck_federated_identities_idp_type", "idp_type IN ('enterprise', 'social')"));

        builder.HasKey(f => f.FederatedIdentityId);
        builder.Property(f => f.FederatedIdentityId)
            .HasColumnName("federated_identity_id")
            .ValueGeneratedNever();

        builder.Property(f => f.UserId).HasColumnName("user_id").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(f => f.UserId);

        builder.Property(f => f.IdpType)
            .HasColumnName("idp_type")
            .HasConversion(EnumDbValueConverters.FederatedIdpType)
            .IsRequired();

        builder.Property(f => f.IdpKey).HasColumnName("idp_key").IsRequired();
        builder.Property(f => f.ExternalSubjectId).HasColumnName("external_subject_id").IsRequired();

        builder.HasIndex(f => new { f.IdpType, f.IdpKey, f.ExternalSubjectId })
            .IsUnique()
            .HasDatabaseName("uq_federated_identities_external");

        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
