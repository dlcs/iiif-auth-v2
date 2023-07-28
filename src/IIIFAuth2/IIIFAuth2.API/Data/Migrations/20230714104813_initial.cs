using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIIFAuth2.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    configuration = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    customer = table.Column<int>(type: "integer", nullable: false),
                    access_service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => new { x.id, x.customer });
                });

            migrationBuilder.CreateTable(
                name: "session_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cookie_id = table.Column<string>(type: "text", nullable: false),
                    access_token = table.Column<string>(type: "text", nullable: false),
                    expires = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    roles = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "access_services",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer = table.Column<int>(type: "integer", nullable: false),
                    role_provider_id = table.Column<Guid>(type: "uuid", nullable: true),
                    child_access_service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_access_service_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "text", nullable: false),
                    profile = table.Column<string>(type: "text", nullable: true),
                    label = table.Column<string>(type: "text", nullable: true),
                    heading = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    confirm_label = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_access_services", x => x.id);
                    table.ForeignKey(
                        name: "fk_access_services_access_services_parent_access_service_id",
                        column: x => x.parent_access_service_id,
                        principalTable: "access_services",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_access_services_role_providers_role_provider_id",
                        column: x => x.role_provider_id,
                        principalTable: "role_providers",
                        principalColumn: "id");
                });

            migrationBuilder.CreateIndex(
                name: "ix_access_services_parent_access_service_id",
                table: "access_services",
                column: "parent_access_service_id");

            migrationBuilder.CreateIndex(
                name: "ix_access_services_role_provider_id",
                table: "access_services",
                column: "role_provider_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_services");

            migrationBuilder.DropTable(
                name: "roles");

            migrationBuilder.DropTable(
                name: "session_users");

            migrationBuilder.DropTable(
                name: "role_providers");
        }
    }
}
