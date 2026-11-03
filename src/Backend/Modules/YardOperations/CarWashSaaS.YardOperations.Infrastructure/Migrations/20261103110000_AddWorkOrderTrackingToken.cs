using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderTrackingToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                schema: "yard",
                table: "WorkOrders",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValueSql: "LOWER(REPLACE(NEWID(), '-', ''))");

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TrackingToken",
                schema: "yard",
                table: "WorkOrders",
                column: "TrackingToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_TrackingToken",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "TrackingToken",
                schema: "yard",
                table: "WorkOrders");
        }
    }
}

