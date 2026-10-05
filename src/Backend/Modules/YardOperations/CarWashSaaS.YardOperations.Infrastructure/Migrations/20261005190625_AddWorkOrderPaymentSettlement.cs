using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkOrderPaymentSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaid",
                schema: "yard",
                table: "WorkOrders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                schema: "yard",
                table: "WorkOrders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAtUtc",
                schema: "yard",
                table: "WorkOrders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                schema: "yard",
                table: "WorkOrders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentTransactionId",
                schema: "yard",
                table: "WorkOrders",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TenantId_IsPaid",
                schema: "yard",
                table: "WorkOrders",
                columns: new[] { "TenantId", "IsPaid" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_TenantId_IsPaid",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "IsPaid",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PaymentTransactionId",
                schema: "yard",
                table: "WorkOrders");
        }
    }
}
