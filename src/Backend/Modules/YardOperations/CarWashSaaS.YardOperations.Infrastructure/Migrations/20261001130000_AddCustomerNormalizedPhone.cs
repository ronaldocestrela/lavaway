using CarWashSaaS.YardOperations.Infrastructure;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CarWashSaaS.YardOperations.Infrastructure.Migrations;

[DbContext(typeof(YardOperationsDbContext))]
[Migration("20261001130000_AddCustomerNormalizedPhone")]
public sealed class AddCustomerNormalizedPhone : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "NormalizedPhone",
            schema: "yard",
            table: "Customers",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.Sql("UPDATE [yard].[Customers] SET [NormalizedPhone] = [Phone];");
        migrationBuilder.Sql("""
            WHILE EXISTS (
                SELECT 1
                FROM [yard].[Customers]
                WHERE [NormalizedPhone] COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%')
            BEGIN
                UPDATE [yard].[Customers]
                SET [NormalizedPhone] = STUFF(
                    [NormalizedPhone],
                    PATINDEX('%[^0-9]%', [NormalizedPhone] COLLATE Latin1_General_100_BIN2),
                    1,
                    '')
                WHERE [NormalizedPhone] COLLATE Latin1_General_100_BIN2 LIKE '%[^0-9]%';
            END;
            """);
        migrationBuilder.Sql("""
            UPDATE [yard].[Customers]
            SET [NormalizedPhone] = SUBSTRING([NormalizedPhone], 3, LEN([NormalizedPhone]) - 2)
            WHERE [NormalizedPhone] COLLATE Latin1_General_100_BIN2 LIKE '55%'
                AND LEN([NormalizedPhone]) IN (12, 13);
            """);

        migrationBuilder.AlterColumn<string>(
            name: "NormalizedPhone",
            schema: "yard",
            table: "Customers",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32,
            oldNullable: true);

        migrationBuilder.DropIndex(
            name: "IX_Customers_TenantId_Phone",
            schema: "yard",
            table: "Customers");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_TenantId_NormalizedPhone",
            schema: "yard",
            table: "Customers",
            columns: new[] { "TenantId", "NormalizedPhone" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_Customers_TenantId_NormalizedPhone",
            schema: "yard",
            table: "Customers");

        migrationBuilder.CreateIndex(
            name: "IX_Customers_TenantId_Phone",
            schema: "yard",
            table: "Customers",
            columns: new[] { "TenantId", "Phone" });

        migrationBuilder.DropColumn(
            name: "NormalizedPhone",
            schema: "yard",
            table: "Customers");
    }
}
