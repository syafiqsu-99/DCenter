using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter_6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_ConsumableTxn_Type",
                table: "DCenter_ConsumableTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_Consumables_Category",
                table: "DCenter_Consumables");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_Consumables_Type",
                table: "DCenter_Consumables");

            migrationBuilder.DropColumn(
                name: "WorkOrderNumber",
                table: "DCenter_ConsumableTransactions");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "DCenter_Consumables");

            migrationBuilder.Sql("""
                UPDATE dbo.DCenter_ConsumableTransactions
                SET TxnType = CASE WHEN QuantityKg > 0 THEN N'Receive' ELSE N'Issue' END,
                    ReferenceNo = N'STOCK TAKE'
                WHERE TxnType = N'Adjust';

                UPDATE dbo.DCenter_Consumables
                SET ConsumableType = CASE WHEN ConsumableType = N'SMAW Electrode' THEN N'Electrode Filler'
                                          ELSE N'Bare & Powder Filler' END
                WHERE ConsumableType NOT IN (N'Bare & Powder Filler', N'Electrode Filler');
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_ConsumableTxn_Type",
                table: "DCenter_ConsumableTransactions",
                sql: "[TxnType] IN (N'Receive', N'Issue', N'Void')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_Consumables_Type",
                table: "DCenter_Consumables",
                sql: "[ConsumableType] IN (N'Bare & Powder Filler', N'Electrode Filler')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_ConsumableTxn_Type",
                table: "DCenter_ConsumableTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_Consumables_Type",
                table: "DCenter_Consumables");

            migrationBuilder.AddColumn<string>(
                name: "WorkOrderNumber",
                table: "DCenter_ConsumableTransactions",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "DCenter_Consumables",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_ConsumableTxn_Type",
                table: "DCenter_ConsumableTransactions",
                sql: "[TxnType] IN (N'Receive', N'Issue', N'Adjust', N'Void')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_Consumables_Category",
                table: "DCenter_Consumables",
                sql: "[Category] IN (N'Bare & Powder Filler', N'Electrode')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_Consumables_Type",
                table: "DCenter_Consumables",
                sql: "[ConsumableType] IN (N'GTAW Filler', N'GMAW Wire', N'SAW Wire', N'SMAW Electrode')");
        }
    }
}
