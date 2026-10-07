using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Identity.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlatformUsersAndAdministrativeAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdministrativeAuditEvents",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimestampUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ActorRole = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ActorRealm = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TargetId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    DetailsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdministrativeAuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformUsers",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastLoginAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformUsers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_Action",
                schema: "identity",
                table: "AdministrativeAuditEvents",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_ActorId",
                schema: "identity",
                table: "AdministrativeAuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_TargetType_TargetId",
                schema: "identity",
                table: "AdministrativeAuditEvents",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_TenantId",
                schema: "identity",
                table: "AdministrativeAuditEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AdministrativeAuditEvents_TimestampUtc",
                schema: "identity",
                table: "AdministrativeAuditEvents",
                column: "TimestampUtc");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformUsers_Email",
                schema: "identity",
                table: "PlatformUsers",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdministrativeAuditEvents",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "PlatformUsers",
                schema: "identity");
        }
    }
}
