using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "DCenter_BakingNoSeq");

            migrationBuilder.CreateSequence(
                name: "DCenter_ConsumableTxnSeq");

            migrationBuilder.CreateSequence(
                name: "DCenter_HoldingNoSeq");

            migrationBuilder.AddColumn<string>(
                name: "UsageScope",
                table: "DCenter_Welders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Report");

            migrationBuilder.CreateTable(
                name: "DCenter_ConsumableItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Specification = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Diameter = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MinStockKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    ActivatedMinKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FinishThresholdKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    HoldingOvenType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_ConsumableItems", x => x.Id);
                    table.CheckConstraint("CK_DCenter_ConsumableItems_Category", "[Category] IN (N'Bare & Powder Filler', N'Electrode Filler')");
                    table.CheckConstraint("CK_DCenter_ConsumableItems_Limits", "[MinStockKg] >= 0 AND [ActivatedMinKg] >= 0 AND ([FinishThresholdKg] IS NULL OR [FinishThresholdKg] >= 0)");
                    table.CheckConstraint("CK_DCenter_ConsumableItems_OvenType", "[HoldingOvenType] IS NULL OR [HoldingOvenType] IN (N'Alloy Steel', N'Mild Steel', N'Ni Alloy', N'Stainless Steel')");
                });

            migrationBuilder.CreateTable(
                name: "DCenter_Ovens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OvenType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LayoutImagePath = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_Ovens", x => x.Id);
                    table.CheckConstraint("CK_DCenter_Ovens_Type", "[OvenType] IN (N'Alloy Steel', N'Mild Steel', N'Ni Alloy', N'Stainless Steel')");
                });

            migrationBuilder.CreateTable(
                name: "DCenter_ConsumableItemLots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ItemId = table.Column<int>(type: "int", nullable: false),
                    Brand = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LotNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_ConsumableItemLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableItemLots_DCenter_ConsumableItems_ItemId",
                        column: x => x.ItemId,
                        principalTable: "DCenter_ConsumableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_OvenCompartments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OvenId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LayoutX = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LayoutY = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LayoutW = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    LayoutH = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_OvenCompartments", x => x.Id);
                    table.CheckConstraint("CK_DCenter_OvenCompartments_Number", "[Number] BETWEEN 1 AND 99");
                    table.ForeignKey(
                        name: "FK_DCenter_OvenCompartments_DCenter_Ovens_OvenId",
                        column: x => x.OvenId,
                        principalTable: "DCenter_Ovens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_BakingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BakingNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: false),
                    QuantityKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    PersonInCharge = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    BakingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BakeStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BakeStop = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RebakeStart = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RebakeStop = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_BakingRecords", x => x.Id);
                    table.CheckConstraint("CK_DCenter_BakingRecords_Qty", "[QuantityKg] > 0");
                    table.CheckConstraint("CK_DCenter_BakingRecords_Status", "[Status] IN (N'Queued', N'Baking', N'Baked', N'RebakeQueued', N'Rebaking', N'Rebaked', N'Closed', N'Cancelled')");
                    table.CheckConstraint("CK_DCenter_BakingRecords_Times", "([BakeStop] IS NULL OR ([BakeStart] IS NOT NULL AND [BakeStop] > [BakeStart])) AND ([RebakeStart] IS NULL OR ([BakeStop] IS NOT NULL AND [RebakeStart] > [BakeStop])) AND ([RebakeStop] IS NULL OR ([RebakeStart] IS NOT NULL AND [RebakeStop] > [RebakeStart]))");
                    table.ForeignKey(
                        name: "FK_DCenter_BakingRecords_DCenter_ConsumableItemLots_LotId",
                        column: x => x.LotId,
                        principalTable: "DCenter_ConsumableItemLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_ConsumableMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TxnNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TxnType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TxnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LotId = table.Column<int>(type: "int", nullable: false),
                    QuantityKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    FromStage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStage = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FromCompartmentId = table.Column<int>(type: "int", nullable: true),
                    ToCompartmentId = table.Column<int>(type: "int", nullable: true),
                    BakingRecordId = table.Column<int>(type: "int", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Requestor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WelderId = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    CountedQtyKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    ReferenceNo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    VoidsMovementId = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_ConsumableMovements", x => x.Id);
                    table.CheckConstraint("CK_DCenter_ConsumableMovements_Bin", "([FromCompartmentId] IS NULL OR [FromStage] = N'Activated') AND ([ToCompartmentId] IS NULL OR [ToStage] = N'Activated')");
                    table.CheckConstraint("CK_DCenter_ConsumableMovements_HoldMove", "([TxnType] <> N'Hold' OR [ToCompartmentId] IS NOT NULL) AND ([TxnType] <> N'Move' OR ([FromStage] = N'Activated' AND [ToStage] = N'Activated' AND [ToCompartmentId] IS NOT NULL))");
                    table.CheckConstraint("CK_DCenter_ConsumableMovements_Qty", "[QuantityKg] > 0");
                    table.CheckConstraint("CK_DCenter_ConsumableMovements_Source", "([Source] IS NULL OR [Source] IN (N'Weld Shop', N'Tool Crib')) AND ([TxnType] <> N'Receive' OR [Source] IS NOT NULL)");
                    table.CheckConstraint("CK_DCenter_ConsumableMovements_Stage", "([FromStage] IS NULL OR [FromStage] IN (N'Normal', N'Baking', N'Activated')) AND ([ToStage] IS NULL OR [ToStage] IN (N'Normal', N'Baking', N'Activated')) AND ([FromStage] IS NOT NULL OR [ToStage] IS NOT NULL)");
                    table.CheckConstraint("CK_DCenter_ConsumableMovements_Type", "[TxnType] IN (N'Receive', N'Transfer', N'SendToBake', N'Hold', N'Move', N'Issue', N'Return', N'Finish', N'Adjust', N'Dispose', N'Void')");
                    table.CheckConstraint("CK_DCenter_ConsumableMovements_Void", "([TxnType] = N'Void' AND [VoidsMovementId] IS NOT NULL) OR ([TxnType] <> N'Void' AND [VoidsMovementId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableMovements_DCenter_BakingRecords_BakingRecordId",
                        column: x => x.BakingRecordId,
                        principalTable: "DCenter_BakingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableMovements_DCenter_ConsumableItemLots_LotId",
                        column: x => x.LotId,
                        principalTable: "DCenter_ConsumableItemLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableMovements_DCenter_ConsumableMovements_VoidsMovementId",
                        column: x => x.VoidsMovementId,
                        principalTable: "DCenter_ConsumableMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableMovements_DCenter_OvenCompartments_FromCompartmentId",
                        column: x => x.FromCompartmentId,
                        principalTable: "DCenter_OvenCompartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableMovements_DCenter_OvenCompartments_ToCompartmentId",
                        column: x => x.ToCompartmentId,
                        principalTable: "DCenter_OvenCompartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_ConsumableMovements_DCenter_Welders_WelderId",
                        column: x => x.WelderId,
                        principalTable: "DCenter_Welders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_HoldingRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HoldingNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    HoldingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BakingRecordId = table.Column<int>(type: "int", nullable: false),
                    WelderId = table.Column<int>(type: "int", nullable: true),
                    WelderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CompartmentId = table.Column<int>(type: "int", nullable: true),
                    IsFinishedAfterBaking = table.Column<bool>(type: "bit", nullable: false),
                    QuantityKg = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    TxnNo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_HoldingRecords", x => x.Id);
                    table.CheckConstraint("CK_DCenter_HoldingRecords_Qty", "[QuantityKg] > 0");
                    table.CheckConstraint("CK_DCenter_HoldingRecords_Target", "([CompartmentId] IS NOT NULL AND [IsFinishedAfterBaking] = 0) OR ([CompartmentId] IS NULL AND [IsFinishedAfterBaking] = 1 AND [WelderId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_DCenter_HoldingRecords_DCenter_BakingRecords_BakingRecordId",
                        column: x => x.BakingRecordId,
                        principalTable: "DCenter_BakingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_HoldingRecords_DCenter_OvenCompartments_CompartmentId",
                        column: x => x.CompartmentId,
                        principalTable: "DCenter_OvenCompartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DCenter_HoldingRecords_DCenter_Welders_WelderId",
                        column: x => x.WelderId,
                        principalTable: "DCenter_Welders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "DCenter_Ovens",
                columns: new[] { "Id", "Code", "IsActive", "LayoutImagePath", "Name", "OvenType", "SortOrder" },
                values: new object[,]
                {
                    { 1, "AS", true, null, "Alloy Steel Oven", "Alloy Steel", 1 },
                    { 2, "MS", true, null, "Mild Steel Oven", "Mild Steel", 2 },
                    { 3, "NI", true, null, "Ni Alloy Oven", "Ni Alloy", 3 },
                    { 4, "SS", true, null, "Stainless Steel Oven", "Stainless Steel", 4 }
                });

            migrationBuilder.InsertData(
                table: "DCenter_OvenCompartments",
                columns: new[] { "Id", "IsActive", "Label", "LayoutH", "LayoutW", "LayoutX", "LayoutY", "Number", "OvenId" },
                values: new object[,]
                {
                    { 1, true, "C1", null, null, null, null, 1, 1 },
                    { 2, true, "C2", null, null, null, null, 2, 1 },
                    { 3, true, "C3", null, null, null, null, 3, 1 },
                    { 4, true, "C4", null, null, null, null, 4, 1 },
                    { 5, true, "C5", null, null, null, null, 5, 1 },
                    { 6, true, "C6", null, null, null, null, 6, 1 },
                    { 7, true, "C7", null, null, null, null, 7, 1 },
                    { 8, true, "C8", null, null, null, null, 8, 1 },
                    { 9, true, "C9", null, null, null, null, 9, 1 },
                    { 10, true, "C1", null, null, null, null, 1, 2 },
                    { 11, true, "C2", null, null, null, null, 2, 2 },
                    { 12, true, "C3", null, null, null, null, 3, 2 },
                    { 13, true, "C4", null, null, null, null, 4, 2 },
                    { 14, true, "C5", null, null, null, null, 5, 2 },
                    { 15, true, "C6", null, null, null, null, 6, 2 },
                    { 16, true, "C7", null, null, null, null, 7, 2 },
                    { 17, true, "C8", null, null, null, null, 8, 2 },
                    { 18, true, "C9", null, null, null, null, 9, 2 },
                    { 19, true, "C1", null, null, null, null, 1, 3 },
                    { 20, true, "C2", null, null, null, null, 2, 3 },
                    { 21, true, "C3", null, null, null, null, 3, 3 },
                    { 22, true, "C4", null, null, null, null, 4, 3 },
                    { 23, true, "C5", null, null, null, null, 5, 3 },
                    { 24, true, "C6", null, null, null, null, 6, 3 },
                    { 25, true, "C7", null, null, null, null, 7, 3 },
                    { 26, true, "C8", null, null, null, null, 8, 3 },
                    { 27, true, "C9", null, null, null, null, 9, 3 },
                    { 28, true, "C1", null, null, null, null, 1, 4 },
                    { 29, true, "C2", null, null, null, null, 2, 4 },
                    { 30, true, "C3", null, null, null, null, 3, 4 },
                    { 31, true, "C4", null, null, null, null, 4, 4 },
                    { 32, true, "C5", null, null, null, null, 5, 4 },
                    { 33, true, "C6", null, null, null, null, 6, 4 },
                    { 34, true, "C7", null, null, null, null, 7, 4 },
                    { 35, true, "C8", null, null, null, null, 8, 4 },
                    { 36, true, "C9", null, null, null, null, 9, 4 }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_Welders_UsageScope",
                table: "DCenter_Welders",
                sql: "[UsageScope] IN (N'Report', N'ReportAndStock')");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BakingRecords_BakingDate",
                table: "DCenter_BakingRecords",
                column: "BakingDate");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BakingRecords_BakingNo",
                table: "DCenter_BakingRecords",
                column: "BakingNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BakingRecords_LotId",
                table: "DCenter_BakingRecords",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BakingRecords_Status",
                table: "DCenter_BakingRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableItemLots_ItemId_Brand_LotNumber",
                table: "DCenter_ConsumableItemLots",
                columns: new[] { "ItemId", "Brand", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableItems_Specification_Diameter",
                table: "DCenter_ConsumableItems",
                columns: new[] { "Specification", "Diameter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_BakingRecordId",
                table: "DCenter_ConsumableMovements",
                column: "BakingRecordId",
                filter: "[BakingRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_CreatedAt",
                table: "DCenter_ConsumableMovements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_FromCompartmentId",
                table: "DCenter_ConsumableMovements",
                column: "FromCompartmentId",
                filter: "[FromCompartmentId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "LotId", "QuantityKg", "IsVoided", "TxnType" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_LotId",
                table: "DCenter_ConsumableMovements",
                column: "LotId")
                .Annotation("SqlServer:Include", new[] { "TxnType", "FromStage", "ToStage", "QuantityKg", "IsVoided" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_ToCompartmentId",
                table: "DCenter_ConsumableMovements",
                column: "ToCompartmentId",
                filter: "[ToCompartmentId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "LotId", "QuantityKg", "IsVoided", "TxnType" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_TxnDate",
                table: "DCenter_ConsumableMovements",
                column: "TxnDate")
                .Annotation("SqlServer:Include", new[] { "TxnType", "QuantityKg", "IsVoided", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_TxnNo",
                table: "DCenter_ConsumableMovements",
                column: "TxnNo");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_VoidsMovementId",
                table: "DCenter_ConsumableMovements",
                column: "VoidsMovementId",
                unique: true,
                filter: "[VoidsMovementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_WelderId_TxnDate",
                table: "DCenter_ConsumableMovements",
                columns: new[] { "WelderId", "TxnDate" },
                filter: "[WelderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_HoldingRecords_BakingRecordId",
                table: "DCenter_HoldingRecords",
                column: "BakingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_HoldingRecords_CompartmentId",
                table: "DCenter_HoldingRecords",
                column: "CompartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_HoldingRecords_HoldingDate",
                table: "DCenter_HoldingRecords",
                column: "HoldingDate");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_HoldingRecords_HoldingNo",
                table: "DCenter_HoldingRecords",
                column: "HoldingNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_HoldingRecords_TxnNo",
                table: "DCenter_HoldingRecords",
                column: "TxnNo");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_HoldingRecords_WelderId",
                table: "DCenter_HoldingRecords",
                column: "WelderId");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_OvenCompartments_OvenId_Number",
                table: "DCenter_OvenCompartments",
                columns: new[] { "OvenId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Ovens_Code",
                table: "DCenter_Ovens",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Ovens_Name",
                table: "DCenter_Ovens",
                column: "Name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DCenter_ConsumableMovements");

            migrationBuilder.DropTable(
                name: "DCenter_HoldingRecords");

            migrationBuilder.DropTable(
                name: "DCenter_BakingRecords");

            migrationBuilder.DropTable(
                name: "DCenter_OvenCompartments");

            migrationBuilder.DropTable(
                name: "DCenter_ConsumableItemLots");

            migrationBuilder.DropTable(
                name: "DCenter_Ovens");

            migrationBuilder.DropTable(
                name: "DCenter_ConsumableItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_Welders_UsageScope",
                table: "DCenter_Welders");

            migrationBuilder.DropColumn(
                name: "UsageScope",
                table: "DCenter_Welders");

            migrationBuilder.DropSequence(
                name: "DCenter_BakingNoSeq");

            migrationBuilder.DropSequence(
                name: "DCenter_ConsumableTxnSeq");

            migrationBuilder.DropSequence(
                name: "DCenter_HoldingNoSeq");
        }
    }
}
