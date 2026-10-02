using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialYardOperations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "yard");

        migrationBuilder.CreateTable(
            name: "Customers",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Phone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Customers", x => x.Id);
                table.UniqueConstraint("AK_Customers_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Services",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Services", x => x.Id);
                table.UniqueConstraint("AK_Services_TenantId_Id", x => new { x.TenantId, x.Id });
            });

        migrationBuilder.CreateTable(
            name: "Vehicles",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Plate = table.Column<string>(type: "varchar(7)", unicode: false, maxLength: 7, nullable: false),
                Size = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Vehicles", x => x.Id);
                table.UniqueConstraint("AK_Vehicles_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Vehicles_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalSchema: "yard",
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "ServicePrices",
            schema: "yard",
            columns: table => new
            {
                VehicleSize = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                EstimatedDurationMinutes = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ServicePrices", x => new { x.ServiceId, x.VehicleSize });
                table.ForeignKey(
                    name: "FK_ServicePrices_Services_ServiceId",
                    column: x => x.ServiceId,
                    principalSchema: "yard",
                    principalTable: "Services",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WorkOrders",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                VehicleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkOrders", x => x.Id);
                table.UniqueConstraint("AK_WorkOrders_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_WorkOrders_Customers_TenantId_CustomerId",
                    columns: x => new { x.TenantId, x.CustomerId },
                    principalSchema: "yard",
                    principalTable: "Customers",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_WorkOrders_Vehicles_TenantId_VehicleId",
                    columns: x => new { x.TenantId, x.VehicleId },
                    principalSchema: "yard",
                    principalTable: "Vehicles",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "WorkOrderItems",
            schema: "yard",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                ServiceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                EstimatedDurationMinutes = table.Column<int>(type: "int", nullable: false),
                Quantity = table.Column<int>(type: "int", nullable: false),
                WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WorkOrderItems", x => x.Id);
                table.ForeignKey(
                    name: "FK_WorkOrderItems_WorkOrders_WorkOrderId",
                    column: x => x.WorkOrderId,
                    principalSchema: "yard",
                    principalTable: "WorkOrders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Customers_TenantId_Phone",
            schema: "yard",
            table: "Customers",
            columns: new[] { "TenantId", "Phone" });

        migrationBuilder.CreateIndex(
            name: "IX_ServicePrices_TenantId_VehicleSize",
            schema: "yard",
            table: "ServicePrices",
            columns: new[] { "TenantId", "VehicleSize" });

        migrationBuilder.CreateIndex(
            name: "IX_Vehicles_TenantId_CustomerId",
            schema: "yard",
            table: "Vehicles",
            columns: new[] { "TenantId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_Vehicles_TenantId_Plate",
            schema: "yard",
            table: "Vehicles",
            columns: new[] { "TenantId", "Plate" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WorkOrderItems_TenantId_ServiceId",
            schema: "yard",
            table: "WorkOrderItems",
            columns: new[] { "TenantId", "ServiceId" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkOrderItems_WorkOrderId",
            schema: "yard",
            table: "WorkOrderItems",
            column: "WorkOrderId");

        migrationBuilder.CreateIndex(
            name: "IX_WorkOrders_TenantId_CreatedAtUtc",
            schema: "yard",
            table: "WorkOrders",
            columns: new[] { "TenantId", "CreatedAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkOrders_TenantId_CustomerId",
            schema: "yard",
            table: "WorkOrders",
            columns: new[] { "TenantId", "CustomerId" });

        migrationBuilder.CreateIndex(
            name: "IX_WorkOrders_TenantId_VehicleId",
            schema: "yard",
            table: "WorkOrders",
            columns: new[] { "TenantId", "VehicleId" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ServicePrices",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "WorkOrderItems",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "Services",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "WorkOrders",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "Vehicles",
            schema: "yard");

        migrationBuilder.DropTable(
            name: "Customers",
            schema: "yard");
    }
}
