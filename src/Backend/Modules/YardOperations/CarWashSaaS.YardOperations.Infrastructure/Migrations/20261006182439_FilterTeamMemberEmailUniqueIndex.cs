using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FilterTeamMemberEmailUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "yard",
                table: "TeamMembers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_TenantId_Email",
                schema: "yard",
                table: "TeamMembers");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TenantId_Email",
                schema: "yard",
                table: "TeamMembers",
                columns: new[] { "TenantId", "Email" },
                unique: true,
                filter: "[Email] IS NOT NULL AND [Email] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_TenantId_Email",
                schema: "yard",
                table: "TeamMembers");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TenantId_Email",
                schema: "yard",
                table: "TeamMembers",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                schema: "yard",
                table: "TeamMembers",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: string.Empty,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
