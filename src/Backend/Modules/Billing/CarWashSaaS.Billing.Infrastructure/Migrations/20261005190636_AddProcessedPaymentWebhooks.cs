using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedPaymentWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessedPaymentWebhooks",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TxId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ProcessedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PayloadHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessedPaymentWebhooks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedPaymentWebhooks_TenantId_Provider_EventId",
                schema: "billing",
                table: "ProcessedPaymentWebhooks",
                columns: new[] { "TenantId", "Provider", "EventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedPaymentWebhooks_TenantId_TxId",
                schema: "billing",
                table: "ProcessedPaymentWebhooks",
                columns: new[] { "TenantId", "TxId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessedPaymentWebhooks",
                schema: "billing");
        }
    }
}
