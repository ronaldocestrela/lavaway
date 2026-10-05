using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.WhatsApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatbotSessionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatbotSessions",
                schema: "whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CurrentStep = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SelectedServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SelectedServiceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SelectedVehicleSize = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SelectedPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SelectedDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SelectedTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    VehiclePlate = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    LastInteractionAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatbotSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatbotSessions_TenantId_CustomerPhone_IsActive",
                schema: "whatsapp",
                table: "ChatbotSessions",
                columns: new[] { "TenantId", "CustomerPhone", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatbotSessions",
                schema: "whatsapp");
        }
    }
}
