using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderSecondaryOperator : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedSecondaryOperatorId",
                schema: "yard",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedSecondaryOperatorName",
                schema: "yard",
                table: "WorkOrders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AssignedSecondaryOperatorName",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "AssignedSecondaryOperatorId",
                schema: "yard",
                table: "WorkOrders");
        }
    }
}

