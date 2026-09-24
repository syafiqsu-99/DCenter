using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DCenter.Server.Migrations
{
    /// <inheritdoc />
    public partial class DCenter9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_OvenCompartments_Number",
                table: "DCenter_OvenCompartments");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DCenter_Ovens");

            migrationBuilder.DropColumn(
                name: "LayoutImagePath",
                table: "DCenter_Ovens");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "DCenter_Ovens");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "DCenter_OvenCompartments");

            migrationBuilder.DropColumn(
                name: "LayoutH",
                table: "DCenter_OvenCompartments");

            migrationBuilder.DropColumn(
                name: "LayoutW",
                table: "DCenter_OvenCompartments");

            migrationBuilder.DropColumn(
                name: "LayoutX",
                table: "DCenter_OvenCompartments");

            migrationBuilder.DropColumn(
                name: "LayoutY",
                table: "DCenter_OvenCompartments");

            migrationBuilder.CreateIndex(
                name: "IX_DCenter_Ovens_OvenType",
                table: "DCenter_Ovens",
                column: "OvenType",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_OvenCompartments_Number",
                table: "DCenter_OvenCompartments",
                sql: "[Number] BETWEEN 1 AND 9");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DCenter_Ovens_OvenType",
                table: "DCenter_Ovens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DCenter_OvenCompartments_Number",
                table: "DCenter_OvenCompartments");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DCenter_Ovens",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LayoutImagePath",
                table: "DCenter_Ovens",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "DCenter_Ovens",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "DCenter_OvenCompartments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutH",
                table: "DCenter_OvenCompartments",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutW",
                table: "DCenter_OvenCompartments",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutX",
                table: "DCenter_OvenCompartments",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LayoutY",
                table: "DCenter_OvenCompartments",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 5,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 9,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 10,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 12,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 13,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 14,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 15,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 16,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 18,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 20,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 21,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 22,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 23,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 24,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 25,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 26,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 27,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 28,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 29,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 30,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 33,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 34,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 35,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_OvenCompartments",
                keyColumn: "Id",
                keyValue: 36,
                columns: new[] { "IsActive", "LayoutH", "LayoutW", "LayoutX", "LayoutY" },
                values: new object[] { true, null, null, null, null });

            migrationBuilder.UpdateData(
                table: "DCenter_Ovens",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "IsActive", "LayoutImagePath", "SortOrder" },
                values: new object[] { true, null, 1 });

            migrationBuilder.UpdateData(
                table: "DCenter_Ovens",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "IsActive", "LayoutImagePath", "SortOrder" },
                values: new object[] { true, null, 2 });

            migrationBuilder.UpdateData(
                table: "DCenter_Ovens",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "IsActive", "LayoutImagePath", "SortOrder" },
                values: new object[] { true, null, 3 });

            migrationBuilder.UpdateData(
                table: "DCenter_Ovens",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "IsActive", "LayoutImagePath", "SortOrder" },
                values: new object[] { true, null, 4 });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DCenter_OvenCompartments_Number",
                table: "DCenter_OvenCompartments",
                sql: "[Number] BETWEEN 1 AND 99");
        }
    }
}
