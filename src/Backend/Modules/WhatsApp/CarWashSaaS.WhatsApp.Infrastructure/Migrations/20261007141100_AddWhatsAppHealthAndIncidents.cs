using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.WhatsApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppHealthAndIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AlertCount",
                schema: "whatsapp",
                table: "WhatsAppConnections",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DisconnectReason",
                schema: "whatsapp",
                table: "WhatsAppConnections",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HasActiveAlert",
                schema: "whatsapp",
                table: "WhatsAppConnections",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastAlertSentAtUtc",
                schema: "whatsapp",
                table: "WhatsAppConnections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastConnectedAtUtc",
                schema: "whatsapp",
                table: "WhatsAppConnections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastDisconnectedAtUtc",
                schema: "whatsapp",
                table: "WhatsAppConnections",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WhatsAppConnectionIncidents",
                schema: "whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProviderSessionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    AlertDispatched = table.Column<bool>(type: "bit", nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppConnectionIncidents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppConnectionIncidents_TenantId_OccurredAtUtc",
                schema: "whatsapp",
                table: "WhatsAppConnectionIncidents",
                columns: new[] { "TenantId", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WhatsAppConnectionIncidents",
                schema: "whatsapp");

            migrationBuilder.DropColumn(
                name: "AlertCount",
                schema: "whatsapp",
                table: "WhatsAppConnections");

            migrationBuilder.DropColumn(
                name: "DisconnectReason",
                schema: "whatsapp",
                table: "WhatsAppConnections");

            migrationBuilder.DropColumn(
                name: "HasActiveAlert",
                schema: "whatsapp",
                table: "WhatsAppConnections");

            migrationBuilder.DropColumn(
                name: "LastAlertSentAtUtc",
                schema: "whatsapp",
                table: "WhatsAppConnections");

            migrationBuilder.DropColumn(
                name: "LastConnectedAtUtc",
                schema: "whatsapp",
                table: "WhatsAppConnections");

            migrationBuilder.DropColumn(
                name: "LastDisconnectedAtUtc",
                schema: "whatsapp",
                table: "WhatsAppConnections");
        }
    }
}
