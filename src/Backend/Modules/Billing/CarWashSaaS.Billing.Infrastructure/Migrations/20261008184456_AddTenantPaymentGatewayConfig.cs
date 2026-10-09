using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPaymentGatewayConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantPaymentGatewayConfigs",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provider = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PagarMeSecretKeyEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PagarMePublicKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PagarMeWebhookSecretEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTestedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastTestSuccess = table.Column<bool>(type: "bit", nullable: true),
                    LastTestMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPaymentGatewayConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantPaymentGatewayConfigs_TenantId",
                schema: "billing",
                table: "TenantPaymentGatewayConfigs",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantPaymentGatewayConfigs",
                schema: "billing");
        }
    }
}
