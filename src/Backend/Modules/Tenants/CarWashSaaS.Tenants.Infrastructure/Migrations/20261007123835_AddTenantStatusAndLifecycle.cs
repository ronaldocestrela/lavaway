using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.Tenants.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantStatusAndLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "tenants",
                table: "Tenants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "StatusChangedAtUtc",
                schema: "tenants",
                table: "Tenants",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "StatusReason",
                schema: "tenants",
                table: "Tenants",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "TrialEndsAtUtc",
                schema: "tenants",
                table: "Tenants",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                schema: "tenants",
                table: "Tenants",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Status",
                schema: "tenants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "tenants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StatusChangedAtUtc",
                schema: "tenants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StatusReason",
                schema: "tenants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TrialEndsAtUtc",
                schema: "tenants",
                table: "Tenants");
        }
    }
}
