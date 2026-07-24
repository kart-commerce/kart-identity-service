using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddServicePrincipals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_principals",
                columns: table => new
                {
                    client_id = table.Column<string>(type: "text", nullable: false),
                    client_secret_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_principals", x => x.client_id);
                    table.CheckConstraint("ck_service_principals_role", "role IN ('admin', 'partner_api')");
                    table.CheckConstraint("ck_service_principals_status", "status IN ('active', 'revoked')");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_principals");
        }
    }
}
