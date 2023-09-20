using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIIFAuth2.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class addcustomerCookieDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "customer_cookie_domains",
                columns: table => new
                {
                    customer = table.Column<int>(type: "integer", nullable: false),
                    domains = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_customer_cookie_domains", x => x.customer);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "customer_cookie_domains");
        }
    }
}
