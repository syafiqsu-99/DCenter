using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DCenter_WpsItems_WpsNo",
                table: "DCenter_WpsItems");

            migrationBuilder.CreateTable(
                name: "DCenter_SupervisorRevokedTokens",
                columns: table => new
                {
                    Fingerprint = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DCenter_SupervisorRevokedTokens", x => x.Fingerprint);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Reports_UpdatedAt",
                table: "DCenter_Reports",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_ConsumableMovements_ReferenceNo",
                table: "DCenter_ConsumableMovements",
                column: "ReferenceNo",
                filter: "[ReferenceNo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_SupervisorRevokedTokens_ExpiresAt",
                table: "DCenter_SupervisorRevokedTokens",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DCenter_SupervisorRevokedTokens");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_Reports_UpdatedAt",
                table: "DCenter_Reports");

            migrationBuilder.DropIndex(
                name: "IX_DCenter_ConsumableMovements_ReferenceNo",
                table: "DCenter_ConsumableMovements");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_WpsItems_WpsNo",
                table: "DCenter_WpsItems",
                column: "WpsNo");
        }
    }
}
