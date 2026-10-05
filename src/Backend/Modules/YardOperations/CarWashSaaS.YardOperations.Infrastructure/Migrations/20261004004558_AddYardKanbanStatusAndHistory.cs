using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddYardKanbanStatusAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedOperatorId",
                schema: "yard",
                table: "WorkOrders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedOperatorName",
                schema: "yard",
                table: "WorkOrders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkOrderStatusHistories",
                schema: "yard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedByOperatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ChangedByOperatorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderStatusHistories_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "yard",
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrders_TenantId_Status",
                schema: "yard",
                table: "WorkOrders",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderStatusHistories_TenantId_WorkOrderId",
                schema: "yard",
                table: "WorkOrderStatusHistories",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderStatusHistories_WorkOrderId",
                schema: "yard",
                table: "WorkOrderStatusHistories",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderStatusHistories",
                schema: "yard");

            migrationBuilder.DropIndex(
                name: "IX_WorkOrders_TenantId_Status",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "AssignedOperatorId",
                schema: "yard",
                table: "WorkOrders");

            migrationBuilder.DropColumn(
                name: "AssignedOperatorName",
                schema: "yard",
                table: "WorkOrders");
        }
    }
}
