using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSaasBillingAndQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedSaasWebhookEvents",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedSaasWebhookEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaasInvoices",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GatewayInvoiceId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DueDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PixQrCode = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    PixCopiaECola = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaasInvoices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SaasPlans",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tier = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxWorkOrdersPerCycle = table.Column<int>(type: "int", nullable: false),
                    MaxWhatsAppMessagesPerCycle = table.Column<int>(type: "int", nullable: false),
                    HasCustomerSubscriptions = table.Column<bool>(type: "bit", nullable: false),
                    HasLoyalty = table.Column<bool>(type: "bit", nullable: false),
                    HasCommissions = table.Column<bool>(type: "bit", nullable: false),
                    HasAiChatbot = table.Column<bool>(type: "bit", nullable: false),
                    MaxTeamMembers = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SaasPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantQuotaUsages",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CycleStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CycleEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WorkOrdersCreatedCount = table.Column<int>(type: "int", nullable: false),
                    WhatsAppMessagesSentCount = table.Column<int>(type: "int", nullable: false),
                    LastUpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantQuotaUsages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSaasSubscriptions",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanTier = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentPeriodStartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentPeriodEndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    GracePeriodEndsAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextBillingDateUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    GatewayCustomerId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    GatewaySubscriptionId = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    StatusReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSaasSubscriptions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedSaasWebhookEvents_EventId",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SaasInvoices_GatewayInvoiceId",
                schema: "billing",
                table: "SaasInvoices",
                column: "GatewayInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SaasInvoices_TenantId",
                schema: "billing",
                table: "SaasInvoices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SaasPlans_Tier",
                schema: "billing",
                table: "SaasPlans",
                column: "Tier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantQuotaUsages_TenantId",
                schema: "billing",
                table: "TenantQuotaUsages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSaasSubscriptions_TenantId",
                schema: "billing",
                table: "TenantSaasSubscriptions",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedSaasWebhookEvents",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "SaasInvoices",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "SaasPlans",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "TenantQuotaUsages",
                schema: "billing");

            migrationBuilder.DropTable(
                name: "TenantSaasSubscriptions",
                schema: "billing");
        }
    }
}
