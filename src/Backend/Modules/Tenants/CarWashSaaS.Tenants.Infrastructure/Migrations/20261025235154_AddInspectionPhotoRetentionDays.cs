using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionPhotoRetentionDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "InspectionPhotoRetentionDays",
                schema: "tenants",
                table: "StoreProfiles",
                type: "int",
                nullable: false,
                defaultValue: 90);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "InspectionPhotoRetentionDays",
                schema: "tenants",
                table: "StoreProfiles");
        }
    }
}
