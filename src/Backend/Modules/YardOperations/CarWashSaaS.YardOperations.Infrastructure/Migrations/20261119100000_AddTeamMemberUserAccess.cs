using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTeamMemberUserAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                schema: "yard",
                table: "TeamMembers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SystemRole",
                schema: "yard",
                table: "TeamMembers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TenantId_UserId",
                schema: "yard",
                table: "TeamMembers",
                columns: new[] { "TenantId", "UserId" },
                filter: "[UserId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_TenantId_UserId",
                schema: "yard",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "SystemRole",
                schema: "yard",
                table: "TeamMembers");

            migrationBuilder.DropColumn(
                name: "UserId",
                schema: "yard",
                table: "TeamMembers");
        }
    }
}
