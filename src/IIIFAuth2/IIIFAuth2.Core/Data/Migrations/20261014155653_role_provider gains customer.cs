using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IIIFAuth2.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class role_providergainscustomer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "customer",
                table: "role_providers",
                type: "integer",
                nullable: false,
                defaultValue: -1);

            // Backfill customer from any associated access_service, leaving the -1 default for
            // role_providers that aren't referenced by an access_service
            migrationBuilder.Sql(@"
update role_providers rp
set customer = sub.customer
from (
    select role_provider_id, min(customer) as customer
    from access_services
    where role_provider_id is not null
    group by role_provider_id
) sub
where rp.id = sub.role_provider_id;");

            // Default was only required for backfilling existing rows
            migrationBuilder.Sql("alter table role_providers alter column customer drop default;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "customer",
                table: "role_providers");
        }
    }
}
