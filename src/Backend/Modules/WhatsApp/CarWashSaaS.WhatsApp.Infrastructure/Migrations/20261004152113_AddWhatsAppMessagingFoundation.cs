using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.WhatsApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppMessagingFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CustomerCommunicationPreferences",
                schema: "whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NormalizedPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsOptedIn = table.Column<bool>(type: "bit", nullable: false),
                    OptedOutAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerCommunicationPreferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantWhatsAppQuotas",
                schema: "whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaxMessagesPerMinute = table.Column<int>(type: "int", nullable: false),
                    MaxMessagesPerDay = table.Column<int>(type: "int", nullable: false),
                    SentInCurrentMinute = table.Column<int>(type: "int", nullable: false),
                    SentToday = table.Column<int>(type: "int", nullable: false),
                    CurrentMinuteWindowUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CurrentDayWindowUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWhatsAppQuotas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppMessages",
                schema: "whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", maxLength: 4096, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WhatsAppDeliveryAttempts",
                schema: "whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboundWhatsAppMessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttemptNumber = table.Column<int>(type: "int", nullable: false),
                    AttemptedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsSuccess = table.Column<bool>(type: "bit", nullable: false),
                    ErrorCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    HttpStatusCode = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WhatsAppDeliveryAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WhatsAppDeliveryAttempts_WhatsAppMessages_OutboundWhatsAppMessageId",
                        column: x => x.OutboundWhatsAppMessageId,
                        principalSchema: "whatsapp",
                        principalTable: "WhatsAppMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerCommunicationPreferences_TenantId_NormalizedPhone",
                schema: "whatsapp",
                table: "CustomerCommunicationPreferences",
                columns: new[] { "TenantId", "NormalizedPhone" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantWhatsAppQuotas_TenantId",
                schema: "whatsapp",
                table: "TenantWhatsAppQuotas",
                column: "TenantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppDeliveryAttempts_OutboundWhatsAppMessageId",
                schema: "whatsapp",
                table: "WhatsAppDeliveryAttempts",
                column: "OutboundWhatsAppMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_CreatedAt",
                schema: "whatsapp",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WhatsAppMessages_TenantId_IdempotencyKey",
                schema: "whatsapp",
                table: "WhatsAppMessages",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomerCommunicationPreferences",
                schema: "whatsapp");

            migrationBuilder.DropTable(
                name: "TenantWhatsAppQuotas",
                schema: "whatsapp");

            migrationBuilder.DropTable(
                name: "WhatsAppDeliveryAttempts",
                schema: "whatsapp");

            migrationBuilder.DropTable(
                name: "WhatsAppMessages",
                schema: "whatsapp");
        }
    }
}
