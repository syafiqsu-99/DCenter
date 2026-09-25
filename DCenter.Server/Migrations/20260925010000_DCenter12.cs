using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE i SET Diameter = n.NewDiameter
                FROM dbo.DCenter_ConsumableItems i
                CROSS APPLY (SELECT FORMAT(TRY_CAST(REPLACE(REPLACE(REPLACE(LOWER(i.Diameter), 'mm', ''), ' ', ''), ',', '.') AS decimal(9,2)),
                                           '0.00', 'en-US') AS NewDiameter) n
                WHERE n.NewDiameter IS NOT NULL
                  AND n.NewDiameter <> i.Diameter
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.DCenter_ConsumableItems o
                      CROSS APPLY (SELECT FORMAT(TRY_CAST(REPLACE(REPLACE(REPLACE(LOWER(o.Diameter), 'mm', ''), ' ', ''), ',', '.') AS decimal(9,2)),
                                                 '0.00', 'en-US') AS NewDiameter) m
                      WHERE o.Id <> i.Id
                        AND o.Specification = i.Specification
                        AND (o.Diameter = n.NewDiameter OR m.NewDiameter = n.NewDiameter));

                DECLARE @leftovers int = (
                    SELECT COUNT(*)
                    FROM dbo.DCenter_ConsumableItems
                    WHERE FORMAT(TRY_CAST(REPLACE(REPLACE(REPLACE(LOWER(Diameter), 'mm', ''), ' ', ''), ',', '.') AS decimal(9,2)),
                                 '0.00', 'en-US') <> Diameter);
                IF @leftovers > 0
                    PRINT CONCAT('DCenter12: ', @leftovers, ' consumable(s) clash with an existing Specification + Diameter and were left ',
                                 'as they are. List them with the query in the DCenter12 migration notes and merge them by hand.');
                """);

            migrationBuilder.Sql("""
                WITH sized AS (
                    SELECT Id, Value, IsActive, SortOrder,
                           FORMAT(TRY_CAST(REPLACE(REPLACE(REPLACE(LOWER(Value), 'mm', ''), ' ', ''), ',', '.') AS decimal(9,2)),
                                  '0.00', 'en-US') AS NewValue
                    FROM dbo.DCenter_Lookups
                    WHERE Category = N'Size'
                ),
                ranked AS (
                    SELECT Id, ROW_NUMBER() OVER (
                        PARTITION BY NewValue
                        ORDER BY IsActive DESC, CASE WHEN Value = NewValue THEN 0 ELSE 1 END, SortOrder, Id) AS Rn
                    FROM sized
                    WHERE NewValue IS NOT NULL
                )
                DELETE l
                FROM dbo.DCenter_Lookups l
                JOIN ranked r ON r.Id = l.Id
                WHERE r.Rn > 1;

                UPDATE l SET Value = n.NewValue
                FROM dbo.DCenter_Lookups l
                CROSS APPLY (SELECT FORMAT(TRY_CAST(REPLACE(REPLACE(REPLACE(LOWER(l.Value), 'mm', ''), ' ', ''), ',', '.') AS decimal(9,2)),
                                           '0.00', 'en-US') AS NewValue) n
                WHERE l.Category = N'Size'
                  AND n.NewValue IS NOT NULL
                  AND n.NewValue <> l.Value;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
