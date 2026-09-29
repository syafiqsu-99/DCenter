using DCenter.Server.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter18 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // One read view per table (V_DCenter_<Table>); stored procedures read through these.
            SqlScripts.Run(migrationBuilder, SqlScripts.Folder("Views", "v1"));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var file in SqlScripts.Folder("Views", "v1"))
                migrationBuilder.Sql($"DROP VIEW IF EXISTS dbo.{SqlScripts.ObjectName(file)};");
        }
    }
}
