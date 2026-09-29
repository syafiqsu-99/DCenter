using DCenter.Server.Data;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter19 : Migration
    {
        private static readonly string[] Types = SqlScripts.Matching("Types", "v1",
            "TT_DCenter_IdList", "TT_DCenter_IdOrder", "TT_DCenter_LookupRows", "TT_DCenter_WpsRows", "TT_DCenter_MrnRows", "TT_DCenter_BpvcRows");

        private static readonly string[] Procedures = SqlScripts.Matching("Procedures", "v1",
            "SP_DCenter_Welder_", "SP_DCenter_Lookup_", "SP_DCenter_ProcessTypeLink_", "SP_DCenter_Wps_", "SP_DCenter_Mrn_", "SP_DCenter_Bpvc_");

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Settings: welders, dropdown lists, Process–Type links and the WPS / MRN / BPVC IX tables.
            SqlScripts.Run(migrationBuilder, Types);
            SqlScripts.Run(migrationBuilder, Procedures);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var file in Procedures)
                migrationBuilder.Sql($"DROP PROCEDURE IF EXISTS dbo.{SqlScripts.ObjectName(file)};");
            foreach (var file in Types)
                migrationBuilder.Sql($"DROP TYPE IF EXISTS dbo.{SqlScripts.ObjectName(file)};");
        }
    }
}
