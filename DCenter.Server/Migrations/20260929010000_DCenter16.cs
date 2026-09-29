using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter16 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE [DCenter_MrnSpecs] SET [SpecNoRaw] = [SpecNo] WHERE [SpecNoRaw] IS NULL OR LTRIM(RTRIM([SpecNoRaw])) = N'';");
            migrationBuilder.Sql("UPDATE [DCenter_BpvcIx] SET [SpecNoRaw] = [SpecNo] WHERE [SpecNoRaw] IS NULL OR LTRIM(RTRIM([SpecNoRaw])) = N'';");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_MrnSpecs_Mrn",
                table: "DCenter_MrnSpecs");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_PNo",
                table: "DCenter_BpvcIx");

            migrationBuilder.DropColumn(
                name: "SpecNo",
                table: "DCenter_MrnSpecs");

            migrationBuilder.DropColumn(
                name: "SpecNo",
                table: "DCenter_BpvcIx");

            migrationBuilder.RenameColumn(
                name: "SpecNoRaw",
                table: "DCenter_MrnSpecs",
                newName: "SpecNo");

            migrationBuilder.RenameColumn(
                name: "SpecNoRaw",
                table: "DCenter_BpvcIx",
                newName: "SpecNo");

            migrationBuilder.AlterColumn<string>(
                name: "SpecNo",
                table: "DCenter_MrnSpecs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "SpecNo",
                table: "DCenter_BpvcIx",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_MrnSpecs_Mrn_SpecNo",
                table: "DCenter_MrnSpecs",
                columns: new[] { "Mrn", "SpecNo" });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_Designation_UnsNo_PNo",
                table: "DCenter_BpvcIx",
                columns: new[] { "SpecNo", "Designation", "UnsNo", "PNo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DCenter_MrnSpecs_Mrn_SpecNo",
                table: "DCenter_MrnSpecs");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_Designation_UnsNo_PNo",
                table: "DCenter_BpvcIx");

            migrationBuilder.AlterColumn<string>(
                name: "SpecNo",
                table: "DCenter_MrnSpecs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "SpecNo",
                table: "DCenter_BpvcIx",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.RenameColumn(
                name: "SpecNo",
                table: "DCenter_MrnSpecs",
                newName: "SpecNoRaw");

            migrationBuilder.RenameColumn(
                name: "SpecNo",
                table: "DCenter_BpvcIx",
                newName: "SpecNoRaw");

            migrationBuilder.AddColumn<string>(
                name: "SpecNo",
                table: "DCenter_MrnSpecs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SpecNo",
                table: "DCenter_BpvcIx",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("UPDATE [DCenter_MrnSpecs] SET [SpecNo] = ISNULL([SpecNoRaw], N'');");
            migrationBuilder.Sql("UPDATE [DCenter_BpvcIx] SET [SpecNo] = ISNULL([SpecNoRaw], N'');");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_MrnSpecs_Mrn",
                table: "DCenter_MrnSpecs",
                column: "Mrn");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_BpvcIx_SpecNo_PNo",
                table: "DCenter_BpvcIx",
                columns: new[] { "SpecNo", "PNo" });
        }
    }
}
