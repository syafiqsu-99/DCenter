using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DCenter_BpvcIx",
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
                    table.PrimaryKey("PK_DCenter_BpvcIx", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_Lookups",
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
                    table.PrimaryKey("PK_DCenter_Lookups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_MrnSpecs",
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
                    table.PrimaryKey("PK_DCenter_MrnSpecs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportRequired = table.Column<bool>(type: "bit", nullable: false),
                    DateWelded = table.Column<DateOnly>(type: "date", nullable: true),
                    WorkOrder = table.Column<string>(type: "nvarchar(max)", nullable: true),
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
                    table.PrimaryKey("PK_DCenter_Reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_Welders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WelderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WelderNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_Welders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_WpsItems",
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
                    table.PrimaryKey("PK_DCenter_WpsItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_Joints",
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
                    table.PrimaryKey("PK_DCenter_Joints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DCenter_Joints_DCenter_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "DCenter_Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_ReportStatusEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_ReportStatusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DCenter_ReportStatusEvents_DCenter_Reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "DCenter_Reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DCenter_JointMaterials",
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
                    table.PrimaryKey("PK_DCenter_JointMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DCenter_JointMaterials_DCenter_Joints_JointId",
                        column: x => x.JointId,
                        principalTable: "DCenter_Joints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_PNo",
                table: "DCenter_BpvcIx",
                columns: new[] { "SpecNo", "PNo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_JointMaterials_JointId",
                table: "DCenter_JointMaterials",
                column: "JointId");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Joints_ReportId",
                table: "DCenter_Joints",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Lookups_Category_Value",
                table: "DCenter_Lookups",
                columns: new[] { "Category", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_MrnSpecs_Mrn",
                table: "DCenter_MrnSpecs",
                column: "Mrn",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Reports_JobNumber",
                table: "DCenter_Reports",
                column: "JobNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ReportStatusEvents_ReportId",
                table: "DCenter_ReportStatusEvents",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Welders_WelderName",
                table: "DCenter_Welders",
                column: "WelderName");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Welders_WelderNo",
                table: "DCenter_Welders",
                column: "WelderNo");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_WpsItems_PNo",
                table: "DCenter_WpsItems",
                column: "PNo");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_WpsItems_WpsNo",
                table: "DCenter_WpsItems",
                column: "WpsNo");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_WpsItems_WpsNo_PNo",
                table: "DCenter_WpsItems",
                columns: new[] { "WpsNo", "PNo" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DCenter_BpvcIx");

            migrationBuilder.DropTable(
                name: "DCenter_JointMaterials");

            migrationBuilder.DropTable(
                name: "DCenter_Lookups");

            migrationBuilder.DropTable(
                name: "DCenter_MrnSpecs");

            migrationBuilder.DropTable(
                name: "DCenter_ReportStatusEvents");

            migrationBuilder.DropTable(
                name: "DCenter_Welders");

            migrationBuilder.DropTable(
                name: "DCenter_WpsItems");

            migrationBuilder.DropTable(
                name: "DCenter_Joints");

            migrationBuilder.DropTable(
                name: "DCenter_Reports");
        }
    }
}
