using Kart.Identity.Domain.Entities;
using Kart.Identity.Domain.ValueObjects;
using Kart.Identity.Infrastructure.Persistence.Converters;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kart.Identity.Infrastructure.Persistence.Configurations;

/// <summary>database-design.md `outbox_events` — Transactional Outbox.</summary>
public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        // Widened for the User Registration, Login & Authentication Journey flow build — OtpCodeRequested
        // and PasswordResetRequested are new event types this service now publishes; without adding them
        // here, OutboxEvent.Create for either type would fail this CHECK constraint at INSERT time.
        builder.ToTable("outbox_events", t => t.HasCheckConstraint(
            "ck_outbox_events_event_type",
            "event_type IN ('UserRegistered', 'SessionCreated', 'UserAccountUpdated', 'OtpCodeRequested', 'PasswordResetRequested')"));

        builder.HasKey(e => e.EventId);
        builder.Property(e => e.EventId)
            .HasColumnName("event_id")
            .HasConversion(TypedIdValueConverters.For<OutboxEventId>())
            .ValueGeneratedNever();

        // sequence_no (BIGSERIAL in database-design.md) is a DB-generated ordering
        // aid for consumers, not read by this service itself — omitted from the EF
        // model until a consumer/poller ticket actually needs to read it back.
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.TraceParent).HasColumnName("trace_parent");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").IsRequired();

        builder.HasIndex(e => e.OccurredAt)
            .HasDatabaseName("idx_outbox_events_unpublished")
            .HasFilter("published_at IS NULL");
    }
}
