using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `mfa_credentials` — one row per user, `user_id` is the table's own primary key.</summary>
public sealed class MfaCredentialConfiguration : IEntityTypeConfiguration<MfaCredential>
{
    public void Configure(EntityTypeBuilder<MfaCredential> builder)
    {
        builder.ToTable("mfa_credentials", t => t.HasCheckConstraint(
            "ck_mfa_credentials_status", "status IN ('pending', 'active')"));

        builder.HasKey(m => m.UserId);
        builder.Property(m => m.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();
        builder.HasOne<User>().WithOne().HasForeignKey<MfaCredential>(m => m.UserId);

        builder.Property(m => m.EncryptedSecret)
            .HasColumnName("encrypted_secret")
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion(EnumDbValueConverters.MfaCredentialStatus)
            .IsRequired();

        builder.Property(m => m.EnrolledAt).HasColumnName("enrolled_at").IsRequired();
        builder.Property(m => m.PendingExpiresAt).HasColumnName("pending_expires_at");
        builder.Property(m => m.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(m => m.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(m => m.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
