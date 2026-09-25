using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_ConsumableItems_Diameter",
                table: "DCenter_ConsumableItems");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_ConsumableItems_Specification_Diameter",
                table: "DCenter_ConsumableItems");

            migrationBuilder.AlterColumn<string>(
                name: "Diameter",
                table: "DCenter_ConsumableItems",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,2)",
                oldPrecision: 5,
                oldScale: 2);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableItems_Specification_Diameter",
                table: "DCenter_ConsumableItems",
                columns: new[] { "Specification", "Diameter" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_ConsumableItems_Diameter",
                table: "DCenter_ConsumableItems",
                sql: "LEN([Diameter]) > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @mesh int = (
                    SELECT COUNT(*) FROM dbo.DCenter_ConsumableItems WHERE TRY_CAST(Diameter AS decimal(5,2)) IS NULL);
                IF @mesh > 0
                BEGIN
                    DECLARE @message nvarchar(2048) = CONCAT(
                        N'DCenter15 rollback stopped: ', @mesh, N' consumable(s) use a mesh size (e.g. 80/325) as the diameter, which the ',
                        N'numeric column cannot hold. List them with: SELECT Id, Diameter, Specification FROM dbo.DCenter_ConsumableItems ',
                        N'WHERE TRY_CAST(Diameter AS decimal(5,2)) IS NULL;');
                    THROW 50000, @message, 1;
                END;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_ConsumableItems_Diameter",
                table: "DCenter_ConsumableItems");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_ConsumableItems_Specification_Diameter",
                table: "DCenter_ConsumableItems");

            migrationBuilder.AlterColumn<decimal>(
                name: "Diameter",
                table: "DCenter_ConsumableItems",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableItems_Specification_Diameter",
                table: "DCenter_ConsumableItems",
                columns: new[] { "Specification", "Diameter" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_ConsumableItems_Diameter",
                table: "DCenter_ConsumableItems",
                sql: "[Diameter] > 0 AND [Diameter] < 100");
        }
    }
}
