using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dcenter");

            migrationBuilder.CreateSequence(
                name: "DocumentNoSeq",
                schema: "dcenter");

            migrationBuilder.CreateTable(
                name: "BpvcIx",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SpecNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    UnsNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MinTensile = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    GroupNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsoGroup = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BrazingPNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    NominalComposition = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    TypicalProductForm = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NominalThicknessLimits = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BpvcIx", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableItems",
                schema: "dcenter",
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
                    table.PrimaryKey("PK_ConsumableItems", x => x.Id);
                    table.CheckConstraint("CK_ConsumableItems_Category", "[Category] IN (N'Bare & Powder Filler', N'Electrode Filler')");
                    table.CheckConstraint("CK_ConsumableItems_Diameter", "LEN([Diameter]) > 0");
                    table.CheckConstraint("CK_ConsumableItems_Limits", "[MinStockKg] >= 0 AND [ActivatedMinKg] >= 0 AND ([FinishThresholdKg] IS NULL OR [FinishThresholdKg] >= 0)");
                    table.CheckConstraint("CK_ConsumableItems_OvenType", "[HoldingOvenType] IS NULL OR [HoldingOvenType] IN (N'Alloy Steel', N'Mild Steel', N'Ni Alloy', N'Stainless Steel')");
                });

            migrationBuilder.CreateTable(
                name: "Lookups",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lookups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MrnSpecs",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Mrn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Form = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FullSpecification = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    SpecNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MrnSpecs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ovens",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    OvenType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ovens", x => x.Id);
                    table.CheckConstraint("CK_Ovens_Type", "[OvenType] IN (N'Alloy Steel', N'Mild Steel', N'Ni Alloy', N'Stainless Steel')");
                });

            migrationBuilder.CreateTable(
                name: "ProcessTypeLinks",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Process = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessTypeLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Reports",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WorkOrderNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportRequired = table.Column<bool>(type: "bit", nullable: false),
                    DateWelded = table.Column<DateOnly>(type: "date", nullable: true),
                    PartNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaterialSpec1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaterialSpec2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaterialSpec3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Grade1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Grade2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Grade3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PNumber1 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PNumber2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PNumber3 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EngineerSupervisor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QaInspector = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StockCounts",
                schema: "dcenter",
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
                    table.PrimaryKey("PK_StockCounts", x => x.Id);
                    table.CheckConstraint("CK_StockCounts_Kg", "[GainKg] >= 0 AND [LossKg] >= 0");
                    table.CheckConstraint("CK_StockCounts_Scope", "[Scope] IN (N'Normal', N'Activated')");
                });

            migrationBuilder.CreateTable(
                name: "SupervisorCredentials",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupervisorCredentials", x => x.Id);
                    table.CheckConstraint("CK_SupervisorCredentials_Single", "[Id] = 1");
                });

            migrationBuilder.CreateTable(
                name: "SupervisorRevokedTokens",
                schema: "dcenter",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupervisorRevokedTokens", x => x.Fingerprint);
                });

            migrationBuilder.CreateTable(
                name: "Welders",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WelderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WelderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    UsageScope = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Report")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Welders", x => x.Id);
                    table.CheckConstraint("CK_Welders_UsageScope", "[UsageScope] IN (N'Report', N'ReportAndStock')");
                });

            migrationBuilder.CreateTable(
                name: "WpsItems",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WpsNo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BaseMetal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Process = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WpsItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableItemLots",
                schema: "dcenter",
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
                    table.PrimaryKey("PK_ConsumableItemLots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConsumableItemLots_ConsumableItems_ItemId",
                        column: x => x.ItemId,
                        principalSchema: "dcenter",
                        principalTable: "ConsumableItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OvenCompartments",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OvenId = table.Column<int>(type: "int", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OvenCompartments", x => x.Id);
                    table.CheckConstraint("CK_OvenCompartments_Number", "[Number] BETWEEN 1 AND 9");
                    table.ForeignKey(
                        name: "FK_OvenCompartments_Ovens_OvenId",
                        column: x => x.OvenId,
                        principalSchema: "dcenter",
                        principalTable: "Ovens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Joints",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    JointNumber = table.Column<int>(type: "int", nullable: false),
                    PartDescLeft = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartNoLeft = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeatNumberLeft = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartDescRight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PartNoRight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeatNumberRight = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WpsNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Rev = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WelderName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WelderNo = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Joints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Joints_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "dcenter",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReportStatusEvents",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReportStatusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReportStatusEvents_Reports_ReportId",
                        column: x => x.ReportId,
                        principalSchema: "dcenter",
                        principalTable: "Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BakingRecords",
                schema: "dcenter",
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
                    table.PrimaryKey("PK_BakingRecords", x => x.Id);
                    table.CheckConstraint("CK_BakingRecords_Qty", "[QuantityKg] > 0");
                    table.CheckConstraint("CK_BakingRecords_Status", "[Status] IN (N'Queued', N'Baking', N'Baked', N'RebakeQueued', N'Rebaking', N'Rebaked', N'Closed', N'Cancelled')");
                    table.CheckConstraint("CK_BakingRecords_Times", "([BakeStop] IS NULL OR ([BakeStart] IS NOT NULL AND [BakeStop] > [BakeStart])) AND ([RebakeStart] IS NULL OR ([BakeStop] IS NOT NULL AND [RebakeStart] > [BakeStop])) AND ([RebakeStop] IS NULL OR ([RebakeStart] IS NOT NULL AND [RebakeStop] > [RebakeStart]))");
                    table.ForeignKey(
                        name: "FK_BakingRecords_ConsumableItemLots_LotId",
                        column: x => x.LotId,
                        principalSchema: "dcenter",
                        principalTable: "ConsumableItemLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "JointMaterials",
                schema: "dcenter",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JointId = table.Column<int>(type: "int", nullable: false),
                    ColumnNumber = table.Column<int>(type: "int", nullable: false),
                    Process = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Size = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Manuf = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeatLot = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JointMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JointMaterials_Joints_JointId",
                        column: x => x.JointId,
                        principalSchema: "dcenter",
                        principalTable: "Joints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConsumableMovements",
                schema: "dcenter",
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
                    table.PrimaryKey("PK_ConsumableMovements", x => x.Id);
                    table.CheckConstraint("CK_ConsumableMovements_Bin", "([FromCompartmentId] IS NULL OR [FromStage] = N'Activated') AND ([ToCompartmentId] IS NULL OR [ToStage] = N'Activated')");
                    table.CheckConstraint("CK_ConsumableMovements_HoldMove", "([TxnType] <> N'Hold' OR [ToCompartmentId] IS NOT NULL) AND ([TxnType] <> N'Move' OR ([FromStage] = N'Activated' AND [ToStage] = N'Activated' AND [ToCompartmentId] IS NOT NULL))");
                    table.CheckConstraint("CK_ConsumableMovements_Qty", "[QuantityKg] > 0");
                    table.CheckConstraint("CK_ConsumableMovements_Source", "([Source] IS NULL OR [Source] IN (N'Weld Shop', N'Tool Crib')) AND ([TxnType] <> N'Receive' OR [Source] IS NOT NULL)");
                    table.CheckConstraint("CK_ConsumableMovements_Stage", "([FromStage] IS NULL OR [FromStage] IN (N'Normal', N'Baking', N'Activated')) AND ([ToStage] IS NULL OR [ToStage] IN (N'Normal', N'Baking', N'Activated')) AND ([FromStage] IS NOT NULL OR [ToStage] IS NOT NULL)");
                    table.CheckConstraint("CK_ConsumableMovements_Type", "[TxnType] IN (N'Receive', N'Transfer', N'SendToBake', N'Hold', N'Move', N'Issue', N'Return', N'Finish', N'Adjust', N'Dispose', N'Void')");
                    table.CheckConstraint("CK_ConsumableMovements_Void", "([TxnType] = N'Void' AND [VoidsMovementId] IS NOT NULL) OR ([TxnType] <> N'Void' AND [VoidsMovementId] IS NULL)");
                    table.ForeignKey(
                        name: "FK_ConsumableMovements_BakingRecords_BakingRecordId",
                        column: x => x.BakingRecordId,
                        principalSchema: "dcenter",
                        principalTable: "BakingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableMovements_ConsumableItemLots_LotId",
                        column: x => x.LotId,
                        principalSchema: "dcenter",
                        principalTable: "ConsumableItemLots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableMovements_ConsumableMovements_VoidsMovementId",
                        column: x => x.VoidsMovementId,
                        principalSchema: "dcenter",
                        principalTable: "ConsumableMovements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableMovements_OvenCompartments_FromCompartmentId",
                        column: x => x.FromCompartmentId,
                        principalSchema: "dcenter",
                        principalTable: "OvenCompartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableMovements_OvenCompartments_ToCompartmentId",
                        column: x => x.ToCompartmentId,
                        principalSchema: "dcenter",
                        principalTable: "OvenCompartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConsumableMovements_Welders_WelderId",
                        column: x => x.WelderId,
                        principalSchema: "dcenter",
                        principalTable: "Welders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HoldingRecords",
                schema: "dcenter",
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
                    table.PrimaryKey("PK_HoldingRecords", x => x.Id);
                    table.CheckConstraint("CK_HoldingRecords_Qty", "[QuantityKg] > 0");
                    table.CheckConstraint("CK_HoldingRecords_Target", "([CompartmentId] IS NOT NULL AND [IsFinishedAfterBaking] = 0) OR ([CompartmentId] IS NULL AND [IsFinishedAfterBaking] = 1 AND [WelderId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_HoldingRecords_BakingRecords_BakingRecordId",
                        column: x => x.BakingRecordId,
                        principalSchema: "dcenter",
                        principalTable: "BakingRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoldingRecords_OvenCompartments_CompartmentId",
                        column: x => x.CompartmentId,
                        principalSchema: "dcenter",
                        principalTable: "OvenCompartments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HoldingRecords_Welders_WelderId",
                        column: x => x.WelderId,
                        principalSchema: "dcenter",
                        principalTable: "Welders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "dcenter",
                table: "Ovens",
                columns: new[] { "Id", "Code", "Name", "OvenType" },
                values: new object[,]
                {
                    { 1, "AS", "Alloy Steel Oven", "Alloy Steel" },
                    { 2, "MS", "Mild Steel Oven", "Mild Steel" },
                    { 3, "NI", "Ni Alloy Oven", "Ni Alloy" },
                    { 4, "SS", "Stainless Steel Oven", "Stainless Steel" }
                });

            migrationBuilder.InsertData(
                schema: "dcenter",
                table: "OvenCompartments",
                columns: new[] { "Id", "Label", "Number", "OvenId" },
                values: new object[,]
                {
                    { 1, "1", 1, 1 },
                    { 2, "2", 2, 1 },
                    { 3, "3", 3, 1 },
                    { 4, "4", 4, 1 },
                    { 5, "5", 5, 1 },
                    { 6, "6", 6, 1 },
                    { 7, "7", 7, 1 },
                    { 8, "8", 8, 1 },
                    { 9, "9", 9, 1 },
                    { 10, "1", 1, 2 },
                    { 11, "2", 2, 2 },
                    { 12, "3", 3, 2 },
                    { 13, "4", 4, 2 },
                    { 14, "5", 5, 2 },
                    { 15, "6", 6, 2 },
                    { 16, "7", 7, 2 },
                    { 17, "8", 8, 2 },
                    { 18, "9", 9, 2 },
                    { 19, "1", 1, 3 },
                    { 20, "2", 2, 3 },
                    { 21, "3", 3, 3 },
                    { 22, "4", 4, 3 },
                    { 23, "5", 5, 3 },
                    { 24, "6", 6, 3 },
                    { 25, "7", 7, 3 },
                    { 26, "8", 8, 3 },
                    { 27, "9", 9, 3 },
                    { 28, "1", 1, 4 },
                    { 29, "2", 2, 4 },
                    { 30, "3", 3, 4 },
                    { 31, "4", 4, 4 },
                    { 32, "5", 5, 4 },
                    { 33, "6", 6, 4 },
                    { 34, "7", 7, 4 },
                    { 35, "8", 8, 4 },
                    { 36, "9", 9, 4 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BakingRecords_BakingDate",
                schema: "dcenter",
                table: "BakingRecords",
                column: "BakingDate");

            migrationBuilder.CreateIndex(
                name: "IX_BakingRecords_BakingNo",
                schema: "dcenter",
                table: "BakingRecords",
                column: "BakingNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BakingRecords_LotId",
                schema: "dcenter",
                table: "BakingRecords",
                column: "LotId");

            migrationBuilder.CreateIndex(
                name: "IX_BakingRecords_Status",
                schema: "dcenter",
                table: "BakingRecords",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BpvcIx_SpecNo_Designation_UnsNo_PNo",
                schema: "dcenter",
                table: "BpvcIx",
                columns: new[] { "SpecNo", "Designation", "UnsNo", "PNo" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableItemLots_ItemId_Brand_LotNumber",
                schema: "dcenter",
                table: "ConsumableItemLots",
                columns: new[] { "ItemId", "Brand", "LotNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableItems_Specification_Diameter",
                schema: "dcenter",
                table: "ConsumableItems",
                columns: new[] { "Specification", "Diameter" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_BakingRecordId",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "BakingRecordId",
                filter: "[BakingRecordId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_CreatedAt",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_FromCompartmentId",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "FromCompartmentId",
                filter: "[FromCompartmentId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "LotId", "QuantityKg", "IsVoided", "TxnType" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_LotId",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "LotId")
                .Annotation("SqlServer:Include", new[] { "TxnType", "FromStage", "ToStage", "QuantityKg", "IsVoided" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_ReferenceNo",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "ReferenceNo",
                filter: "[ReferenceNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_ToCompartmentId",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "ToCompartmentId",
                filter: "[ToCompartmentId] IS NOT NULL")
                .Annotation("SqlServer:Include", new[] { "LotId", "QuantityKg", "IsVoided", "TxnType" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_TxnDate",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "TxnDate")
                .Annotation("SqlServer:Include", new[] { "TxnType", "QuantityKg", "IsVoided", "LotId" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_TxnNo",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "TxnNo");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_VoidsMovementId",
                schema: "dcenter",
                table: "ConsumableMovements",
                column: "VoidsMovementId",
                unique: true,
                filter: "[VoidsMovementId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ConsumableMovements_WelderId_TxnDate",
                schema: "dcenter",
                table: "ConsumableMovements",
                columns: new[] { "WelderId", "TxnDate" },
                filter: "[WelderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingRecords_BakingRecordId",
                schema: "dcenter",
                table: "HoldingRecords",
                column: "BakingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingRecords_CompartmentId",
                schema: "dcenter",
                table: "HoldingRecords",
                column: "CompartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingRecords_HoldingDate",
                schema: "dcenter",
                table: "HoldingRecords",
                column: "HoldingDate");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingRecords_HoldingNo",
                schema: "dcenter",
                table: "HoldingRecords",
                column: "HoldingNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HoldingRecords_TxnNo",
                schema: "dcenter",
                table: "HoldingRecords",
                column: "TxnNo");

            migrationBuilder.CreateIndex(
                name: "IX_HoldingRecords_WelderId",
                schema: "dcenter",
                table: "HoldingRecords",
                column: "WelderId");

            migrationBuilder.CreateIndex(
                name: "IX_JointMaterials_JointId",
                schema: "dcenter",
                table: "JointMaterials",
                column: "JointId");

            migrationBuilder.CreateIndex(
                name: "IX_Joints_ReportId",
                schema: "dcenter",
                table: "Joints",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_Lookups_Category_Value",
                schema: "dcenter",
                table: "Lookups",
                columns: new[] { "Category", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MrnSpecs_Mrn_SpecNo",
                schema: "dcenter",
                table: "MrnSpecs",
                columns: new[] { "Mrn", "SpecNo" });

            migrationBuilder.CreateIndex(
                name: "IX_OvenCompartments_OvenId_Number",
                schema: "dcenter",
                table: "OvenCompartments",
                columns: new[] { "OvenId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ovens_Code",
                schema: "dcenter",
                table: "Ovens",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ovens_Name",
                schema: "dcenter",
                table: "Ovens",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ovens_OvenType",
                schema: "dcenter",
                table: "Ovens",
                column: "OvenType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessTypeLinks_Process_Type",
                schema: "dcenter",
                table: "ProcessTypeLinks",
                columns: new[] { "Process", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reports_UpdatedAt",
                schema: "dcenter",
                table: "Reports",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_WorkOrderNumber",
                schema: "dcenter",
                table: "Reports",
                column: "WorkOrderNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReportStatusEvents_ReportId",
                schema: "dcenter",
                table: "ReportStatusEvents",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_ReferenceNo",
                schema: "dcenter",
                table: "StockCounts",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockCounts_Scope_CountDate",
                schema: "dcenter",
                table: "StockCounts",
                columns: new[] { "Scope", "CountDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SupervisorRevokedTokens_ExpiresAt",
                schema: "dcenter",
                table: "SupervisorRevokedTokens",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Welders_WelderName",
                schema: "dcenter",
                table: "Welders",
                column: "WelderName");

            migrationBuilder.CreateIndex(
                name: "IX_Welders_WelderNo",
                schema: "dcenter",
                table: "Welders",
                column: "WelderNo");

            migrationBuilder.CreateIndex(
                name: "IX_WpsItems_PNo",
                schema: "dcenter",
                table: "WpsItems",
                column: "PNo");

            migrationBuilder.CreateIndex(
                name: "IX_WpsItems_WpsNo_PNo",
                schema: "dcenter",
                table: "WpsItems",
                columns: new[] { "WpsNo", "PNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BpvcIx",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "ConsumableMovements",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "HoldingRecords",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "JointMaterials",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "Lookups",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "MrnSpecs",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "ProcessTypeLinks",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "ReportStatusEvents",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "StockCounts",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "SupervisorCredentials",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "SupervisorRevokedTokens",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "WpsItems",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "BakingRecords",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "OvenCompartments",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "Welders",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "Joints",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "ConsumableItemLots",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "Ovens",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "Reports",
                schema: "dcenter");

            migrationBuilder.DropTable(
                name: "ConsumableItems",
                schema: "dcenter");

            migrationBuilder.DropSequence(
                name: "DocumentNoSeq",
                schema: "dcenter");
        }
    }
}
