using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxTraceParentAndWidenEventTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_outbox_events_event_type",
                table: "outbox_events");

            migrationBuilder.AddColumn<string>(
                name: "trace_parent",
                table: "outbox_events",
                type: "text",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_outbox_events_event_type",
                table: "outbox_events",
                sql: "event_type IN ('UserRegistered', 'SessionCreated', 'UserAccountUpdated', 'OtpCodeRequested', 'PasswordResetRequested')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_outbox_events_event_type",
                table: "outbox_events");

            migrationBuilder.DropColumn(
                name: "trace_parent",
                table: "outbox_events");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outbox_events_event_type",
                table: "outbox_events",
                sql: "event_type IN ('UserRegistered', 'SessionCreated', 'UserAccountUpdated')");
        }
    }
}
