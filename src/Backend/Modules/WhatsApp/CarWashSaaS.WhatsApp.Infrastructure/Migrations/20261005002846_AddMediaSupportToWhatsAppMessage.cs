using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.WhatsApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaSupportToWhatsAppMessage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MediaFileName",
                schema: "whatsapp",
                table: "WhatsAppMessages",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaMimeType",
                schema: "whatsapp",
                table: "WhatsAppMessages",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaType",
                schema: "whatsapp",
                table: "WhatsAppMessages",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MediaUrlOrBase64",
                schema: "whatsapp",
                table: "WhatsAppMessages",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaFileName",
                schema: "whatsapp",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaMimeType",
                schema: "whatsapp",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaType",
                schema: "whatsapp",
                table: "WhatsAppMessages");

            migrationBuilder.DropColumn(
                name: "MediaUrlOrBase64",
                schema: "whatsapp",
                table: "WhatsAppMessages");
        }
    }
}
