using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.WhatsApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetBookingIdToChatbotSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TargetBookingId",
                schema: "whatsapp",
                table: "ChatbotSessions",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetBookingId",
                schema: "whatsapp",
                table: "ChatbotSessions");
        }
    }
}
