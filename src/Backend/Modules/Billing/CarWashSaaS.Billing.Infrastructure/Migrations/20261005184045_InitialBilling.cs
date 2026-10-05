using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "billing");

            migrationBuilder.CreateTable(
                name: "PixCharges",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TxId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QrCodeBase64 = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CopyPasteKey = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    WhatsAppSentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    PaidAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PixCharges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PixCharges_TenantId_TxId",
                schema: "billing",
                table: "PixCharges",
                columns: new[] { "TenantId", "TxId" });

            migrationBuilder.CreateIndex(
                name: "IX_PixCharges_TenantId_WorkOrderId",
                schema: "billing",
                table: "PixCharges",
                columns: new[] { "TenantId", "WorkOrderId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PixCharges",
                schema: "billing");
        }
    }
}
