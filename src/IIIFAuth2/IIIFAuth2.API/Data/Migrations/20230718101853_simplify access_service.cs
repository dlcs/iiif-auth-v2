using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIIFAuth2.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class simplifyaccess_service : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_access_services_access_services_parent_access_service_id",
                table: "access_services");

            migrationBuilder.DropIndex(
                name: "ix_access_services_parent_access_service_id",
                table: "access_services");

            migrationBuilder.DropColumn(
                name: "child_access_service_id",
                table: "access_services");

            migrationBuilder.DropColumn(
                name: "parent_access_service_id",
                table: "access_services");

            migrationBuilder.AlterColumn<string>(
                name: "profile",
                table: "access_services",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "access_token_error_heading",
                table: "access_services",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "access_token_error_note",
                table: "access_services",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logout_label",
                table: "access_services",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_services_customer_name",
                table: "access_services",
                columns: new[] { "customer", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_access_services_customer_name",
                table: "access_services");

            migrationBuilder.DropColumn(
                name: "access_token_error_heading",
                table: "access_services");

            migrationBuilder.DropColumn(
                name: "access_token_error_note",
                table: "access_services");

            migrationBuilder.DropColumn(
                name: "logout_label",
                table: "access_services");

            migrationBuilder.AlterColumn<string>(
                name: "profile",
                table: "access_services",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<Guid>(
                name: "child_access_service_id",
                table: "access_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "parent_access_service_id",
                table: "access_services",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_access_services_parent_access_service_id",
                table: "access_services",
                column: "parent_access_service_id");

            migrationBuilder.AddForeignKey(
                name: "fk_access_services_access_services_parent_access_service_id",
                table: "access_services",
                column: "parent_access_service_id",
                principalTable: "access_services",
                principalColumn: "id");
        }
    }
}
