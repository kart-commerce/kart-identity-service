using Kart.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `password_reset_tokens`.</summary>
public sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");

        builder.HasKey(t => t.ResetTokenId);
        builder.Property(t => t.ResetTokenId)
            .HasColumnName("reset_token_id")
            .ValueGeneratedNever();

        builder.Property(t => t.UserId).HasColumnName("user_id").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(t => t.UserId);

        builder.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("uq_password_reset_tokens_hash");

        builder.Property(t => t.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();

        // design-decisions.md, "Concurrency Control for Refresh-Token Rotation" —
        // generalized here per database-design.md's own note that reset-confirm
        // uses "the same DB-conditional-update pattern" as refresh-token rotation.
        builder.Property(t => t.ConsumedAt).HasColumnName("consumed_at").IsConcurrencyToken();

        builder.Property(t => t.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").IsRequired();
    }
}
