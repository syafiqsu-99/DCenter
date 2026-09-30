using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenterSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_WpsItems",
                newName: "DCenter_WpsItems",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_Welders",
                newName: "DCenter_Welders",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_SupervisorRevokedTokens",
                newName: "DCenter_SupervisorRevokedTokens",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_SupervisorCredentials",
                newName: "DCenter_SupervisorCredentials",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_StockCounts",
                newName: "DCenter_StockCounts",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_ReportStatusEvents",
                newName: "DCenter_ReportStatusEvents",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_Reports",
                newName: "DCenter_Reports",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_ProcessTypeLinks",
                newName: "DCenter_ProcessTypeLinks",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_Ovens",
                newName: "DCenter_Ovens",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_OvenCompartments",
                newName: "DCenter_OvenCompartments",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_MrnSpecs",
                newName: "DCenter_MrnSpecs",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_Lookups",
                newName: "DCenter_Lookups",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_Joints",
                newName: "DCenter_Joints",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_JointMaterials",
                newName: "DCenter_JointMaterials",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_HoldingRecords",
                newName: "DCenter_HoldingRecords",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_ConsumableMovements",
                newName: "DCenter_ConsumableMovements",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_ConsumableItems",
                newName: "DCenter_ConsumableItems",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_ConsumableItemLots",
                newName: "DCenter_ConsumableItemLots",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_BpvcIx",
                newName: "DCenter_BpvcIx",
                newSchema: "dcenter");

            migrationBuilder.RenameTable(
                name: "DCenter_BakingRecords",
                newName: "DCenter_BakingRecords",
                newSchema: "dcenter");

            migrationBuilder.RenameSequence(
                name: "DCenter_StockCountSeq",
                newName: "DCenter_StockCountSeq",
                newSchema: "dcenter");

            migrationBuilder.RenameSequence(
                name: "DCenter_HoldingNoSeq",
                newName: "DCenter_HoldingNoSeq",
                newSchema: "dcenter");

            migrationBuilder.RenameSequence(
                name: "DCenter_ConsumableTxnSeq",
                newName: "DCenter_ConsumableTxnSeq",
                newSchema: "dcenter");

            migrationBuilder.RenameSequence(
                name: "DCenter_BakingNoSeq",
                newName: "DCenter_BakingNoSeq",
                newSchema: "dcenter");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "DCenter_WpsItems",
                schema: "dcenter",
                newName: "DCenter_WpsItems");

            migrationBuilder.RenameTable(
                name: "DCenter_Welders",
                schema: "dcenter",
                newName: "DCenter_Welders");

            migrationBuilder.RenameTable(
                name: "DCenter_SupervisorRevokedTokens",
                schema: "dcenter",
                newName: "DCenter_SupervisorRevokedTokens");

            migrationBuilder.RenameTable(
                name: "DCenter_SupervisorCredentials",
                schema: "dcenter",
                newName: "DCenter_SupervisorCredentials");

            migrationBuilder.RenameTable(
                name: "DCenter_StockCounts",
                schema: "dcenter",
                newName: "DCenter_StockCounts");

            migrationBuilder.RenameTable(
                name: "DCenter_ReportStatusEvents",
                schema: "dcenter",
                newName: "DCenter_ReportStatusEvents");

            migrationBuilder.RenameTable(
                name: "DCenter_Reports",
                schema: "dcenter",
                newName: "DCenter_Reports");

            migrationBuilder.RenameTable(
                name: "DCenter_ProcessTypeLinks",
                schema: "dcenter",
                newName: "DCenter_ProcessTypeLinks");

            migrationBuilder.RenameTable(
                name: "DCenter_Ovens",
                schema: "dcenter",
                newName: "DCenter_Ovens");

            migrationBuilder.RenameTable(
                name: "DCenter_OvenCompartments",
                schema: "dcenter",
                newName: "DCenter_OvenCompartments");

            migrationBuilder.RenameTable(
                name: "DCenter_MrnSpecs",
                schema: "dcenter",
                newName: "DCenter_MrnSpecs");

            migrationBuilder.RenameTable(
                name: "DCenter_Lookups",
                schema: "dcenter",
                newName: "DCenter_Lookups");

            migrationBuilder.RenameTable(
                name: "DCenter_Joints",
                schema: "dcenter",
                newName: "DCenter_Joints");

            migrationBuilder.RenameTable(
                name: "DCenter_JointMaterials",
                schema: "dcenter",
                newName: "DCenter_JointMaterials");

            migrationBuilder.RenameTable(
                name: "DCenter_HoldingRecords",
                schema: "dcenter",
                newName: "DCenter_HoldingRecords");

            migrationBuilder.RenameTable(
                name: "DCenter_ConsumableMovements",
                schema: "dcenter",
                newName: "DCenter_ConsumableMovements");

            migrationBuilder.RenameTable(
                name: "DCenter_ConsumableItems",
                schema: "dcenter",
                newName: "DCenter_ConsumableItems");

            migrationBuilder.RenameTable(
                name: "DCenter_ConsumableItemLots",
                schema: "dcenter",
                newName: "DCenter_ConsumableItemLots");

            migrationBuilder.RenameTable(
                name: "DCenter_BpvcIx",
                schema: "dcenter",
                newName: "DCenter_BpvcIx");

            migrationBuilder.RenameTable(
                name: "DCenter_BakingRecords",
                schema: "dcenter",
                newName: "DCenter_BakingRecords");

            migrationBuilder.RenameSequence(
                name: "DCenter_StockCountSeq",
                schema: "dcenter",
                newName: "DCenter_StockCountSeq");

            migrationBuilder.RenameSequence(
                name: "DCenter_HoldingNoSeq",
                schema: "dcenter",
                newName: "DCenter_HoldingNoSeq");

            migrationBuilder.RenameSequence(
                name: "DCenter_ConsumableTxnSeq",
                schema: "dcenter",
                newName: "DCenter_ConsumableTxnSeq");

            migrationBuilder.RenameSequence(
                name: "DCenter_BakingNoSeq",
                schema: "dcenter",
                newName: "DCenter_BakingNoSeq");
        }
    }
}
