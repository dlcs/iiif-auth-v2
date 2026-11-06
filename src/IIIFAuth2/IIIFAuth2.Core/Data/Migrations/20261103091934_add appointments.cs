using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIIFAuth2.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "appointments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    customer = table.Column<int>(type: "integer", nullable: false),
                    role_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "text", nullable: false),
                    roles = table.Column<string>(type: "text", nullable: false),
                    start = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    approved_by = table.Column<string>(type: "text", nullable: true),
                    date_approved = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    name = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    asset_patterns = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_appointments", x => x.id);
                    table.ForeignKey(
                        name: "fk_appointments_role_providers_role_provider_id",
                        column: x => x.role_provider_id,
                        principalTable: "role_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_appointments",
                columns: table => new
                {
                    session_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_appointments", x => new { x.session_user_id, x.appointment_id });
                    table.ForeignKey(
                        name: "fk_session_appointments_appointments_appointment_id",
                        column: x => x.appointment_id,
                        principalTable: "appointments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_session_appointments_session_users_session_user_id",
                        column: x => x.session_user_id,
                        principalTable: "session_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_appointments_customer_token",
                table: "appointments",
                columns: new[] { "customer", "token" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_appointments_role_provider_id",
                table: "appointments",
                column: "role_provider_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_appointments_appointment_id",
                table: "session_appointments",
                column: "appointment_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_appointments");

            migrationBuilder.DropTable(
                name: "appointments");
        }
    }
}
