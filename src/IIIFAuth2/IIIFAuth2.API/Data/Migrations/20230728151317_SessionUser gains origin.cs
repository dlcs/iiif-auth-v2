using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIIFAuth2.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class SessionUsergainsorigin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "session_users",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "origin",
                table: "session_users");
        }
    }
}
