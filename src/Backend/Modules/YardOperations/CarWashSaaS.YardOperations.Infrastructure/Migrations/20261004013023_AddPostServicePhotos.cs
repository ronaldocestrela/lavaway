using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPostServicePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceCategory",
                schema: "yard",
                table: "WorkOrderItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "WorkOrderPostServicePhotos",
                schema: "yard",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkOrderItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BeforeInspectionPhotoId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkOrderPostServicePhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkOrderPostServicePhotos_WorkOrders_WorkOrderId",
                        column: x => x.WorkOrderId,
                        principalSchema: "yard",
                        principalTable: "WorkOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderPostServicePhotos_TenantId_WorkOrderId",
                schema: "yard",
                table: "WorkOrderPostServicePhotos",
                columns: new[] { "TenantId", "WorkOrderId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkOrderPostServicePhotos_WorkOrderId",
                schema: "yard",
                table: "WorkOrderPostServicePhotos",
                column: "WorkOrderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkOrderPostServicePhotos",
                schema: "yard");

            migrationBuilder.DropColumn(
                name: "ServiceCategory",
                schema: "yard",
                table: "WorkOrderItems");
        }
    }
}
