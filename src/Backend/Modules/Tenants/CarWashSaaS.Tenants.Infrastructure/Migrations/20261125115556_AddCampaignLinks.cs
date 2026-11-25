using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DestinationPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    UtmSource = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UtmMedium = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UtmCampaign = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UtmTerm = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    UtmContent = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ClickCount = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignLinks_Slug",
                table: "CampaignLinks",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignLinks");
        }
    }
}
