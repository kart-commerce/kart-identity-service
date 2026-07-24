using Kart.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `outbox_events` — Transactional Outbox.</summary>
public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events", t => t.HasCheckConstraint(
            "ck_outbox_events_event_type",
            "event_type IN ('UserRegistered', 'SessionCreated', 'UserAccountUpdated')"));

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedNever();

        // sequence_no (BIGSERIAL in database-design.md) is a DB-generated ordering
        // aid for consumers, not read by this service itself — omitted from the EF
        // model until a consumer/poller ticket actually needs to read it back.
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasIndex(e => e.OccurredAt)
            .HasDatabaseName("idx_outbox_events_unpublished")
            .HasFilter("published_at IS NULL");
    }
}
