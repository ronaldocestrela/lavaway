using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInspectionPhotoPurgeFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPurged",
                schema: "yard",
                table: "InspectionPhotos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PurgeReason",
                schema: "yard",
                table: "InspectionPhotos",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgedAtUtc",
                schema: "yard",
                table: "InspectionPhotos",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_InspectionPhotos_TenantId_IsPurged",
                schema: "yard",
                table: "InspectionPhotos",
                columns: new[] { "TenantId", "IsPurged" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_InspectionPhotos_TenantId_IsPurged",
                schema: "yard",
                table: "InspectionPhotos");

            migrationBuilder.DropColumn(
                name: "IsPurged",
                schema: "yard",
                table: "InspectionPhotos");

            migrationBuilder.DropColumn(
                name: "PurgeReason",
                schema: "yard",
                table: "InspectionPhotos");

            migrationBuilder.DropColumn(
                name: "PurgedAtUtc",
                schema: "yard",
                table: "InspectionPhotos");
        }
    }
}
