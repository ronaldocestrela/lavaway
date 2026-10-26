using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.WhatsApp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantWhatsAppConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantWhatsAppConfigs",
                schema: "whatsapp",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EvolutionApiKeyEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    WebhookSecretEncrypted = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    InstanceCustomName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTestSuccess = table.Column<bool>(type: "bit", nullable: true),
                    LastTestMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantWhatsAppConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantWhatsAppConfigs_TenantId",
                schema: "whatsapp",
                table: "TenantWhatsAppConfigs",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantWhatsAppConfigs",
                schema: "whatsapp");
        }
    }
}
