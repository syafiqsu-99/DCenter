using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter_5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DCenter_MrnSpecs_Mrn",
                table: "DCenter_MrnSpecs");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_PNo",
                table: "DCenter_BpvcIx");

            migrationBuilder.CreateTable(
                name: "DCenter_Consumables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsumableType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Manufacturer = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Diameter = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MinStockKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_Consumables", x => x.Id);
                    table.CheckConstraint("CK_DCenter_Consumables_Category", "[Category] IN (N'Bare & Powder Filler', N'Electrode')");
                    table.CheckConstraint("CK_DCenter_Consumables_MinStock", "[MinStockKg] >= 0");
                    table.CheckConstraint("CK_DCenter_Consumables_Type", "[ConsumableType] IN (N'GTAW Filler', N'GMAW Wire', N'SAW Wire', N'SMAW Electrode')");
                });

            migrationBuilder.CreateTable(
                name: "DCenter_ConsumableLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ConsumableId = table.Column<int>(type: "int", nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_ConsumableLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableLots_DCenter_Consumables_ConsumableId",
                        column: x => x.ConsumableId,
                        principalTable: "DCenter_Consumables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_ConsumableTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LotId = table.Column<int>(type: "int", nullable: false),
                    TxnType = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    TxnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QuantityKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Requestor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    VoidsTxnId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_ConsumableTransactions", x => x.Id);
                    table.CheckConstraint("CK_DCenter_ConsumableTxn_Location", "[Location] IN (N'Weldshop', N'Tool Crib')");
                    table.CheckConstraint("CK_DCenter_ConsumableTxn_Sign", "[QuantityKg] <> 0 AND ([TxnType] <> N'Receive' OR [QuantityKg] > 0) AND ([TxnType] <> N'Issue' OR [QuantityKg] < 0)");
                    table.CheckConstraint("CK_DCenter_ConsumableTxn_Type", "[TxnType] IN (N'Receive', N'Issue', N'Adjust', N'Void')");
                    table.CheckConstraint("CK_DCenter_ConsumableTxn_Void", "([TxnType] = N'Void' AND [VoidsTxnId] IS NOT NULL) OR ([TxnType] <> N'Void' AND [VoidsTxnId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableTransactions_DCenter_ConsumableLots_LotId",
                        column: x => x.LotId,
                        principalTable: "DCenter_ConsumableLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableTransactions_DCenter_ConsumableTransactions_VoidsTxnId",
                        column: x => x.VoidsTxnId,
                        principalTable: "DCenter_ConsumableTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_MrnSpecs_Mrn",
                table: "DCenter_MrnSpecs",
                column: "Mrn");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_PNo",
                table: "DCenter_BpvcIx",
                columns: new[] { "SpecNo", "PNo" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableLots_ConsumableId_LotNumber",
                table: "DCenter_ConsumableLots",
                columns: new[] { "ConsumableId", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Consumables_ConsumableType_Manufacturer_Specification_Diameter",
                table: "DCenter_Consumables",
                columns: new[] { "ConsumableType", "Manufacturer", "Specification", "Diameter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableTransactions_CreatedAt",
                table: "DCenter_ConsumableTransactions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableTransactions_LotId_Location",
                table: "DCenter_ConsumableTransactions",
                columns: new[] { "LotId", "Location" })
                .Annotation("SqlServer:Include", new[] { "TxnType", "QuantityKg", "IsVoided" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableTransactions_TxnDate",
                table: "DCenter_ConsumableTransactions",
                column: "TxnDate")
                .Annotation("SqlServer:Include", new[] { "TxnType", "QuantityKg", "IsVoided", "LotId", "Location" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableTransactions_VoidsTxnId",
                table: "DCenter_ConsumableTransactions",
                column: "VoidsTxnId",
                unique: true,
                filter: "[VoidsTxnId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DCenter_ConsumableTransactions");

            migrationBuilder.DropTable(
                name: "DCenter_ConsumableLots");

            migrationBuilder.DropTable(
                name: "DCenter_Consumables");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_MrnSpecs_Mrn",
                table: "DCenter_MrnSpecs");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_PNo",
                table: "DCenter_BpvcIx");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_MrnSpecs_Mrn",
                table: "DCenter_MrnSpecs",
                column: "Mrn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_PNo",
                table: "DCenter_BpvcIx",
                columns: new[] { "SpecNo", "PNo" },
                unique: true);
        }
    }
}
