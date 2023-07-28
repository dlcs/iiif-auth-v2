using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIIFAuth2.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Addroleprovisiontokensexpandsessionuser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "customer",
                table: "session_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_checked",
                table: "session_users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "role_provision_tokens",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    used = table.Column<bool>(type: "boolean", nullable: false),
                    roles = table.Column<string>(type: "text", nullable: false),
                    created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    customer = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_provision_tokens", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_provision_tokens");

            migrationBuilder.DropColumn(
                name: "customer",
                table: "session_users");

            migrationBuilder.DropColumn(
                name: "last_checked",
                table: "session_users");
        }
    }
}
