using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionsAndRecurringCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerSubscriptions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CurrentPeriodStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentPeriodEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    TotalCreditsInCycle = table.Column<int>(type: "int", nullable: false),
                    UsedCreditsInCycle = table.Column<int>(type: "int", nullable: false),
                    GatewaySubscriptionId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CardLastFourDigits = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CardBrand = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CanceledAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelReason = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreditsPerCycle = table.Column<int>(type: "int", nullable: false),
                    AllowedPlatesLimit = table.Column<int>(type: "int", nullable: false),
                    BillingIntervalDays = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionUsages",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Plate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionUsages_CustomerSubscriptions_CustomerSubscriptionId",
                        column: x => x.CustomerSubscriptionId,
                        principalSchema: "billing",
                        principalTable: "CustomerSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionVehiclePlates",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Plate = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionVehiclePlates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionVehiclePlates_CustomerSubscriptions_CustomerSubscriptionId",
                        column: x => x.CustomerSubscriptionId,
                        principalSchema: "billing",
                        principalTable: "CustomerSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptions_TenantId_CustomerId",
                schema: "billing",
                table: "CustomerSubscriptions",
                columns: new[] { "TenantId", "CustomerId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptions_TenantId_GatewaySubscriptionId",
                schema: "billing",
                table: "CustomerSubscriptions",
                columns: new[] { "TenantId", "GatewaySubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerSubscriptions_TenantId_Status",
                schema: "billing",
                table: "CustomerSubscriptions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_TenantId_IsActive",
                schema: "billing",
                table: "SubscriptionPlans",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUsages_CustomerSubscriptionId",
                schema: "billing",
                table: "SubscriptionUsages",
                column: "CustomerSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUsages_TenantId_CustomerSubscriptionId_ConsumedAtUtc",
                schema: "billing",
                table: "SubscriptionUsages",
                columns: new[] { "TenantId", "CustomerSubscriptionId", "ConsumedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUsages_TenantId_Plate",
                schema: "billing",
                table: "SubscriptionUsages",
                columns: new[] { "TenantId", "Plate" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionUsages_TenantId_WorkOrderId",
                schema: "billing",
                table: "SubscriptionUsages",
                columns: new[] { "TenantId", "WorkOrderId" },
                unique: true,
                filter: "[WorkOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionVehiclePlates_CustomerSubscriptionId",
                schema: "billing",
                table: "SubscriptionVehiclePlates",
                column: "CustomerSubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionVehiclePlates_TenantId_CustomerSubscriptionId",
                schema: "billing",
                table: "SubscriptionVehiclePlates",
                columns: new[] { "TenantId", "CustomerSubscriptionId" });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionVehiclePlates_TenantId_Plate",
                schema: "billing",
                table: "SubscriptionVehiclePlates",
                columns: new[] { "TenantId", "Plate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionPlans",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "SubscriptionUsages",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "SubscriptionVehiclePlates",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "CustomerSubscriptions",
                schema: "billing");
        }
    }
}
