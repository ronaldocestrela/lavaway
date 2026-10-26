using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookPayloadPurgeFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RawPayload",
                schema: "billing",
                table: "ProcessedPaymentWebhooks",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPurged",
                schema: "billing",
                table: "ProcessedPaymentWebhooks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgedAtUtc",
                schema: "billing",
                table: "ProcessedPaymentWebhooks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedPaymentWebhooks_IsPurged_ReceivedAtUtc",
                schema: "billing",
                table: "ProcessedPaymentWebhooks",
                columns: new[] { "IsPurged", "ReceivedAtUtc" });

            migrationBuilder.AddColumn<string>(
                name: "RawPayload",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPurged",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PurgedAtUtc",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessedSaasWebhookEvents_IsPurged_ReceivedAtUtc",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents",
                columns: new[] { "IsPurged", "ReceivedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProcessedPaymentWebhooks_IsPurged_ReceivedAtUtc",
                schema: "billing",
                table: "ProcessedPaymentWebhooks");

            migrationBuilder.DropColumn(
                name: "RawPayload",
                schema: "billing",
                table: "ProcessedPaymentWebhooks");

            migrationBuilder.DropColumn(
                name: "IsPurged",
                schema: "billing",
                table: "ProcessedPaymentWebhooks");

            migrationBuilder.DropColumn(
                name: "PurgedAtUtc",
                schema: "billing",
                table: "ProcessedPaymentWebhooks");

            migrationBuilder.DropIndex(
                name: "IX_ProcessedSaasWebhookEvents_IsPurged_ReceivedAtUtc",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents");

            migrationBuilder.DropColumn(
                name: "RawPayload",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents");

            migrationBuilder.DropColumn(
                name: "IsPurged",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents");

            migrationBuilder.DropColumn(
                name: "PurgedAtUtc",
                schema: "billing",
                table: "ProcessedSaasWebhookEvents");
        }
    }
}
