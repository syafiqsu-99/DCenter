using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddReportCompletedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lookups",
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
                    table.PrimaryKey("PK_lookups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportRequired = table.Column<bool>(type: "bit", nullable: false),
                    DateWelded = table.Column<DateTime>(type: "datetime2", nullable: true),
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
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "welders",
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
                    table.PrimaryKey("PK_welders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "joints",
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
                    table.PrimaryKey("PK_joints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_joints_reports_ReportId",
                        column: x => x.ReportId,
                        principalTable: "reports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "joint_materials",
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
                    table.PrimaryKey("PK_joint_materials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_joint_materials_joints_JointId",
                        column: x => x.JointId,
                        principalTable: "joints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_joint_materials_JointId",
                table: "joint_materials",
                column: "JointId");

            migrationBuilder.CreateIndex(
                name: "IX_joints_ReportId",
                table: "joints",
                column: "ReportId");

            migrationBuilder.CreateIndex(
                name: "IX_lookups_Category_Value",
                table: "lookups",
                columns: new[] { "Category", "Value" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_reports_JobNumber",
                table: "reports",
                column: "JobNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_welders_WelderName",
                table: "welders",
                column: "WelderName");

            migrationBuilder.CreateIndex(
                name: "IX_welders_WelderNo",
                table: "welders",
                column: "WelderNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "joint_materials");

            migrationBuilder.DropTable(
                name: "lookups");

            migrationBuilder.DropTable(
                name: "welders");

            migrationBuilder.DropTable(
                name: "joints");

            migrationBuilder.DropTable(
                name: "reports");
        }
    }
}
