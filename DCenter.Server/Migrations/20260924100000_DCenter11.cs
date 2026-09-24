using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter11 : Migration
    {
        private const int Ovens = 4;
        private const int CompartmentsPerOven = 9;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            SetCompartmentLabels(migrationBuilder, n => n.ToString());

            migrationBuilder.Sql("""
                UPDATE i SET Diameter = n.NewDiameter
                FROM dbo.DCenter_ConsumableItems i
                CROSS APPLY (SELECT FORMAT(TRY_CAST(i.Diameter AS decimal(9,2)), '0.00', 'en-US') AS NewDiameter) n
                WHERE n.NewDiameter IS NOT NULL
                  AND n.NewDiameter <> i.Diameter
                  AND NOT EXISTS (
                      SELECT 1
                      FROM dbo.DCenter_ConsumableItems o
                      CROSS APPLY (SELECT FORMAT(TRY_CAST(o.Diameter AS decimal(9,2)), '0.00', 'en-US') AS NewDiameter) m
                      WHERE o.Id <> i.Id
                        AND o.Specification = i.Specification
                        AND (o.Diameter = n.NewDiameter OR m.NewDiameter = n.NewDiameter));
                """);

            migrationBuilder.Sql("""
                WITH sized AS (
                    SELECT Id, Value, IsActive, SortOrder,
                           FORMAT(TRY_CAST(Value AS decimal(9,2)), '0.00', 'en-US') AS NewValue
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
                CROSS APPLY (SELECT FORMAT(TRY_CAST(l.Value AS decimal(9,2)), '0.00', 'en-US') AS NewValue) n
                WHERE l.Category = N'Size'
                  AND n.NewValue IS NOT NULL
                  AND n.NewValue <> l.Value;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            SetCompartmentLabels(migrationBuilder, n => $"C{n}");
        }

        private static void SetCompartmentLabels(MigrationBuilder migrationBuilder, Func<int, string> label)
        {
            for (var oven = 1; oven <= Ovens; oven++)
            {
                for (var n = 1; n <= CompartmentsPerOven; n++)
                {
                    migrationBuilder.UpdateData(
                        table: "DCenter_OvenCompartments",
                        keyColumn: "Id",
                        keyValue: (oven - 1) * CompartmentsPerOven + n,
                        column: "Label",
                        value: label(n));
                }
            }
        }
    }
}
