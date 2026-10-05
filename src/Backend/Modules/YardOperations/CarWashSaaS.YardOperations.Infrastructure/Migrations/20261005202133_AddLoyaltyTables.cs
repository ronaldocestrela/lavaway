using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerLoyaltyAccounts",
                schema: "yard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Balance = table.Column<int>(type: "int", nullable: false),
                    TotalEarned = table.Column<int>(type: "int", nullable: false),
                    TotalRedeemed = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastAccrualAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastRedemptionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerLoyaltyAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyPrograms",
                schema: "yard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TargetStamps = table.Column<int>(type: "int", nullable: false),
                    RewardTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProximityThreshold = table.Column<int>(type: "int", nullable: false),
                    AllServicesEligible = table.Column<bool>(type: "bit", nullable: false),
                    EligibleCategoryFilter = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyPrograms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyTransactions",
                schema: "yard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerLoyaltyAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<int>(type: "int", nullable: false),
                    BalanceAfter = table.Column<int>(type: "int", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyTransactions_CustomerLoyaltyAccounts_CustomerLoyaltyAccountId",
                        column: x => x.CustomerLoyaltyAccountId,
                        principalSchema: "yard",
                        principalTable: "CustomerLoyaltyAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerLoyaltyAccounts_TenantId_CustomerId",
                schema: "yard",
                table: "CustomerLoyaltyAccounts",
                columns: new[] { "TenantId", "CustomerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyPrograms_TenantId",
                schema: "yard",
                table: "LoyaltyPrograms",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_CustomerLoyaltyAccountId",
                schema: "yard",
                table: "LoyaltyTransactions",
                column: "CustomerLoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_TenantId_CustomerLoyaltyAccountId",
                schema: "yard",
                table: "LoyaltyTransactions",
                columns: new[] { "TenantId", "CustomerLoyaltyAccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyTransactions_TenantId_CustomerLoyaltyAccountId_WorkOrderId",
                schema: "yard",
                table: "LoyaltyTransactions",
                columns: new[] { "TenantId", "CustomerLoyaltyAccountId", "WorkOrderId" },
                unique: true,
                filter: "[WorkOrderId] IS NOT NULL AND [Type] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyPrograms",
                schema: "yard");

            migrationBuilder.DropTable(
                name: "LoyaltyTransactions",
                schema: "yard");

            migrationBuilder.DropTable(
                name: "CustomerLoyaltyAccounts",
                schema: "yard");
        }
    }
}
