using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DECLARE @bad TABLE (Id int PRIMARY KEY);
                INSERT INTO @bad (Id)
                SELECT Id
                FROM dbo.DCenter_ConsumableItems
                WHERE TRY_CAST(Diameter AS decimal(5,2)) IS NULL
                   OR FORMAT(TRY_CAST(Diameter AS decimal(5,2)), '0.00', 'en-US') <> Diameter
                   OR TRY_CAST(Diameter AS decimal(5,2)) <= 0
                   OR TRY_CAST(Diameter AS decimal(5,2)) >= 100;

                DECLARE @withHistory int = (
                    SELECT COUNT(*)
                    FROM @bad b
                    WHERE EXISTS (
                        SELECT 1
                        FROM dbo.DCenter_ConsumableItemLots l
                        WHERE l.ItemId = b.Id
                          AND (EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableMovements m WHERE m.LotId = l.Id)
                               OR EXISTS (SELECT 1 FROM dbo.DCenter_BakingRecords r WHERE r.LotId = l.Id))));

                IF @withHistory > 0
                BEGIN
                    DECLARE @message nvarchar(2048) = CONCAT(
                        N'DCenter14 stopped: ', @withHistory, N' consumable(s) have a diameter that is not a number with 2 decimal places ',
                        N'and already have stock history, so they were not deleted. Fix their Diameter to N.NN (for example 3.20) and run the ',
                        N'migration again. List them with: SELECT Id, Diameter, Specification FROM dbo.DCenter_ConsumableItems ',
                        N'WHERE TRY_CAST(Diameter AS decimal(5,2)) IS NULL OR FORMAT(TRY_CAST(Diameter AS decimal(5,2)), ''0.00'', ''en-US'') <> Diameter;');
                    THROW 50000, @message, 1;
                END;

                DELETE l FROM dbo.DCenter_ConsumableItemLots l JOIN @bad b ON b.Id = l.ItemId;
                DELETE i FROM dbo.DCenter_ConsumableItems i JOIN @bad b ON b.Id = i.Id;
                """);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
        }
    }
}
