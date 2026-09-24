using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "DCenter_StockCountSeq");

            migrationBuilder.CreateTable(
                name: "DCenter_StockCounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CountDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    LinesCounted = table.Column<int>(type: "int", nullable: false),
                    LinesAdjusted = table.Column<int>(type: "int", nullable: false),
                    GainKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    LossKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TxnNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_StockCounts", x => x.Id);
                    table.CheckConstraint("CK_DCenter_StockCounts_Kg", "[GainKg] >= 0 AND [LossKg] >= 0");
                    table.CheckConstraint("CK_DCenter_StockCounts_Scope", "[Scope] IN (N'Normal', N'Activated')");
                });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_StockCounts_ReferenceNo",
                table: "DCenter_StockCounts",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_StockCounts_Scope_CountDate",
                table: "DCenter_StockCounts",
                columns: new[] { "Scope", "CountDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DCenter_StockCounts");

            migrationBuilder.DropSequence(
                name: "DCenter_StockCountSeq");
        }
    }
}
