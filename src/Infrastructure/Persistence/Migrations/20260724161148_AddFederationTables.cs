using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kart.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFederationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "federated_identities",
                columns: table => new
                {
                    federated_identity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idp_type = table.Column<string>(type: "text", nullable: false),
                    idp_key = table.Column<string>(type: "text", nullable: false),
                    external_subject_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_federated_identities", x => x.federated_identity_id);
                    table.CheckConstraint("ck_federated_identities_idp_type", "idp_type IN ('enterprise', 'social')");
                    table.ForeignKey(
                        name: "FK_federated_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "idp_group_role_mappings",
                columns: table => new
                {
                    mapping_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idp_alias = table.Column<string>(type: "text", nullable: false),
                    external_group_claim = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_idp_group_role_mappings", x => x.mapping_id);
                    table.CheckConstraint("ck_idp_group_role_mappings_role", "role IN ('support_agent', 'admin')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_federated_identities_user_id",
                table: "federated_identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "uq_federated_identities_external",
                table: "federated_identities",
                columns: new[] { "idp_type", "idp_key", "external_subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uq_idp_group_role_mappings",
                table: "idp_group_role_mappings",
                columns: new[] { "idp_alias", "external_group_claim" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "federated_identities");

            migrationBuilder.DropTable(
                name: "idp_group_role_mappings");
        }
    }
}
