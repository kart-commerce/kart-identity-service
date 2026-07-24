using Kart.Identity.Domain.Entities;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `sessions` — the `Session` aggregate root.</summary>
public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions", t => t.HasCheckConstraint(
            "ck_sessions_revoked_reason",
            "revoked_reason IS NULL OR revoked_reason IN " +
            "('logout', 'reuse_detected', 'admin_lock', 'role_change', 'password_reset', 'erasure')"));

        builder.HasKey(s => s.SessionId);
        builder.Property(s => s.SessionId)
            .HasColumnName("session_id")
            .ValueGeneratedNever();

        builder.Property(s => s.UserId).HasColumnName("user_id").IsRequired();
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.UserId);

        builder.Property(s => s.IsFederated).HasColumnName("is_federated").IsRequired();
        builder.Property(s => s.IdpAlias).HasColumnName("idp_alias");

        builder.Property(s => s.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(s => s.AbsoluteExpiresAt).HasColumnName("absolute_expires_at").IsRequired();
        builder.Property(s => s.RevokedAt).HasColumnName("revoked_at");

        builder.Property(s => s.RevokedReason)
            .HasColumnName("revoked_reason")
            .HasConversion(EnumDbValueConverters.SessionRevocationReason);

        builder.Property(s => s.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(s => s.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasIndex(s => s.UserId)
            .HasDatabaseName("idx_sessions_user_live")
            .HasFilter("revoked_at IS NULL");
    }
}
