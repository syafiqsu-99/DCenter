using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'dbo.DCenter_ConsumableTransactions', N'U') IS NOT NULL
                   AND EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableTransactions)
                   AND NOT EXISTS (SELECT 1 FROM dbo.DCenter_ConsumableItems)
                    THROW 50000, N'DCenter13 stopped: the legacy consumable tables still hold data that was never copied into DCenter_ConsumableItems. Migrate that data first.', 1;
                """);

            migrationBuilder.DropTable(name: "DCenter_ConsumableTransactions");
            migrationBuilder.DropTable(name: "DCenter_ConsumableLots");
            migrationBuilder.DropTable(name: "DCenter_Consumables");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "DCenter13 dropped the legacy consumable tables and their data. Restore them from a database backup instead.");
        }
    }
}
