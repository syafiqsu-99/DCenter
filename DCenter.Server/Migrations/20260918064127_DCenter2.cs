using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DCenter_Reports_JobNumber",
                table: "DCenter_Reports");

            migrationBuilder.DropColumn(
                name: "WorkOrder",
                table: "DCenter_Reports");

            migrationBuilder.RenameColumn(
                name: "JobNumber",
                table: "DCenter_Reports",
                newName: "WorkOrderNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Reports_WorkOrderNumber",
                table: "DCenter_Reports",
                column: "WorkOrderNumber",
                unique: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecNoRaw",
                table: "DCenter_MrnSpecs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecNoRaw",
                table: "DCenter_BpvcIx",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SpecNoRaw",
                table: "DCenter_MrnSpecs");

            migrationBuilder.DropColumn(
                name: "SpecNoRaw",
                table: "DCenter_BpvcIx");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_Reports_WorkOrderNumber",
                table: "DCenter_Reports");

            migrationBuilder.RenameColumn(
                name: "WorkOrderNumber",
                table: "DCenter_Reports",
                newName: "JobNumber");

            migrationBuilder.AddColumn<string>(
                name: "WorkOrder",
                table: "DCenter_Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("UPDATE [DCenter_Reports] SET [WorkOrder] = [JobNumber];");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Reports_JobNumber",
                table: "DCenter_Reports",
                column: "JobNumber",
                unique: true);
        }
    }
}
