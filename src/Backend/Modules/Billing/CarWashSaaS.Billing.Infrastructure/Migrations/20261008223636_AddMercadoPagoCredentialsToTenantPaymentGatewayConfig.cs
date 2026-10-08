using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMercadoPagoCredentialsToTenantPaymentGatewayConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoAccessTokenEncrypted",
                schema: "billing",
                table: "TenantPaymentGatewayConfigs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoPublicKey",
                schema: "billing",
                table: "TenantPaymentGatewayConfigs",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MercadoPagoWebhookSecretEncrypted",
                schema: "billing",
                table: "TenantPaymentGatewayConfigs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MercadoPagoAccessTokenEncrypted",
                schema: "billing",
                table: "TenantPaymentGatewayConfigs");

            migrationBuilder.DropColumn(
                name: "MercadoPagoPublicKey",
                schema: "billing",
                table: "TenantPaymentGatewayConfigs");

            migrationBuilder.DropColumn(
                name: "MercadoPagoWebhookSecretEncrypted",
                schema: "billing",
                table: "TenantPaymentGatewayConfigs");
        }
    }
}
