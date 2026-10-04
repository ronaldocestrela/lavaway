using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddVehicleInspections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VehicleInspections",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                FuelLevel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                OdometerKm = table.Column<int>(type: "int", nullable: true),
                Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VehicleInspections", x => x.Id);
                table.UniqueConstraint("AK_VehicleInspections_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "InspectionDamages",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                View = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CoordinateX = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                CoordinateY = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                Severity = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionDamages", x => x.Id);
                table.ForeignKey(
                    name: "FK_InspectionDamages_VehicleInspections_VehicleInspectionId",
                    column: x => x.VehicleInspectionId,
                    principalSchema: "yard",
                    principalTable: "VehicleInspections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InspectionChecklistItems",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ItemKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Observation = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionChecklistItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_InspectionChecklistItems_VehicleInspections_VehicleInspectionId",
                    column: x => x.VehicleInspectionId,
                    principalSchema: "yard",
                    principalTable: "VehicleInspections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "InspectionPhotos",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleInspectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                DamageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InspectionPhotos", x => x.Id);
                table.ForeignKey(
                    name: "FK_InspectionPhotos_VehicleInspections_VehicleInspectionId",
                    column: x => x.VehicleInspectionId,
                    principalSchema: "yard",
                    principalTable: "VehicleInspections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VehicleInspections_TenantId_WorkOrderId",
            schema: "yard",
            table: "VehicleInspections",
            columns: new[] { "TenantId", "WorkOrderId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_InspectionDamages_TenantId_VehicleInspectionId",
            schema: "yard",
            table: "InspectionDamages",
            columns: new[] { "TenantId", "VehicleInspectionId" });

        migrationBuilder.CreateIndex(
            name: "IX_InspectionDamages_VehicleInspectionId",
            schema: "yard",
            table: "InspectionDamages",
            column: "VehicleInspectionId");

        migrationBuilder.CreateIndex(
            name: "IX_InspectionChecklistItems_TenantId_VehicleInspectionId",
            schema: "yard",
            table: "InspectionChecklistItems",
            columns: new[] { "TenantId", "VehicleInspectionId" });

        migrationBuilder.CreateIndex(
            name: "IX_InspectionChecklistItems_VehicleInspectionId",
            schema: "yard",
            table: "InspectionChecklistItems",
            column: "VehicleInspectionId");

        migrationBuilder.CreateIndex(
            name: "IX_InspectionPhotos_TenantId_VehicleInspectionId",
            schema: "yard",
            table: "InspectionPhotos",
            columns: new[] { "TenantId", "VehicleInspectionId" });

        migrationBuilder.CreateIndex(
            name: "IX_InspectionPhotos_VehicleInspectionId",
            schema: "yard",
            table: "InspectionPhotos",
            column: "VehicleInspectionId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "InspectionPhotos",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "InspectionChecklistItems",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "InspectionDamages",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "VehicleInspections",
            schema: "yard");
    }
}
