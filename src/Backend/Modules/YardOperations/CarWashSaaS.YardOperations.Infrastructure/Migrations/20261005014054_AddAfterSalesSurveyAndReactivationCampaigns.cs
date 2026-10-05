using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAfterSalesSurveyAndReactivationCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PickedUpAtUtc",
                schema: "yard",
                table: "WorkOrders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SurveyFeedback",
                schema: "yard",
                table: "WorkOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SurveyRating",
                schema: "yard",
                table: "WorkOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SurveyRespondedAtUtc",
                schema: "yard",
                table: "WorkOrders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SurveySentAtUtc",
                schema: "yard",
                table: "WorkOrders",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ReactivationCampaignLogs",
                schema: "yard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignRuleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DaysInactive = table.Column<int>(type: "int", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactivationCampaignLogs", x => x.Id);
                    table.UniqueConstraint("AK_ReactivationCampaignLogs_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateTable(
                name: "ReactivationCampaignRules",
                schema: "yard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DaysInactive = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MessageTemplate = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    PromotionalOffer = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReactivationCampaignRules", x => x.Id);
                    table.UniqueConstraint("AK_ReactivationCampaignRules_TenantId_Id", x => new { x.TenantId, x.Id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TenantId_PickedUpAtUtc_SurveySentAtUtc",
                schema: "yard",
                table: "WorkOrders",
                columns: new[] { "TenantId", "PickedUpAtUtc", "SurveySentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReactivationCampaignLogs_TenantId_CustomerId_SentAtUtc",
                schema: "yard",
                table: "ReactivationCampaignLogs",
                columns: new[] { "TenantId", "CustomerId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReactivationCampaignLogs_TenantId_IdempotencyKey",
                schema: "yard",
                table: "ReactivationCampaignLogs",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReactivationCampaignRules_TenantId_DaysInactive",
                schema: "yard",
                table: "ReactivationCampaignRules",
                columns: new[] { "TenantId", "DaysInactive" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReactivationCampaignLogs",
                schema: "yard");

            migrationBuilder.DropTable(
                name: "ReactivationCampaignRules",
                schema: "yard");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_TenantId_PickedUpAtUtc_SurveySentAtUtc",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "PickedUpAtUtc",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SurveyFeedback",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SurveyRating",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SurveyRespondedAtUtc",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "SurveySentAtUtc",
                schema: "yard",
                table: "WorkOrders");
        }
    }
}
