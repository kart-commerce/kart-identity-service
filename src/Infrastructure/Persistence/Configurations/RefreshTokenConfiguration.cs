using Kart.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `refresh_tokens` — the rotation-chain child of the `Session` aggregate.</summary>
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(t => t.TokenId);
        builder.Property(t => t.TokenId)
            .HasColumnName("token_id")
            .ValueGeneratedNever();

        builder.Property(t => t.SessionId).HasColumnName("session_id").IsRequired();
        builder.HasOne<Session>().WithMany().HasForeignKey(t => t.SessionId);

        builder.Property(t => t.ParentTokenId).HasColumnName("parent_token_id");
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(t => t.ParentTokenId);

        builder.Property(t => t.TokenHash).HasColumnName("token_hash").IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique().HasDatabaseName("uq_refresh_tokens_hash");

        builder.Property(t => t.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(t => t.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(t => t.ConsumedAt).HasColumnName("consumed_at");

        builder.Property(t => t.ReplacedByTokenId).HasColumnName("replaced_by_token_id");
        builder.HasOne<RefreshToken>().WithMany().HasForeignKey(t => t.ReplacedByTokenId);

        builder.Property(t => t.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasIndex(t => t.SessionId).HasDatabaseName("idx_refresh_tokens_session");
    }
}
