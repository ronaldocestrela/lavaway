using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxBoxesToSaasPlan : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxBoxes",
                schema: "billing",
                table: "SaasPlans",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxBoxes",
                schema: "billing",
                table: "SaasPlans");
        }
    }
}
