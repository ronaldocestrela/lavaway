using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.WhatsApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerFieldsToChatbotSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                schema: "whatsapp",
                table: "ChatbotSessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PendingCustomerName",
                schema: "whatsapp",
                table: "ChatbotSessions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerId",
                schema: "whatsapp",
                table: "ChatbotSessions");

            migrationBuilder.DropColumn(
                name: "PendingCustomerName",
                schema: "whatsapp",
                table: "ChatbotSessions");
        }
    }
}
