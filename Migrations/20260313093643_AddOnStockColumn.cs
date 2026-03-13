using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoApp.Migrations
{
    /// <inheritdoc />
    public partial class AddOnStockColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add OnStock column
            migrationBuilder.AddColumn<string>(
                name: "OnStock",
                table: "Photos",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            // Copy existing MonthlyQuantity data to OnStock
            migrationBuilder.Sql("UPDATE Photos SET OnStock = MonthlyQuantity WHERE MonthlyQuantity IS NOT NULL;");

            // Clear MonthlyQuantity for user to fill in manually
            migrationBuilder.Sql("UPDATE Photos SET MonthlyQuantity = NULL;");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 6,
                column: "Value",
                value: "V�eme");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 28,
                column: "Value",
                value: "Kanto��k");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 29,
                column: "Value",
                value: "Ku��lek");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 41,
                column: "Value",
                value: "Po�umavsk�");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OnStock",
                table: "Photos");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 6,
                column: "Value",
                value: "Vážeme");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 28,
                column: "Value",
                value: "Kantořík");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 29,
                column: "Value",
                value: "Kužílek");

            migrationBuilder.UpdateData(
                table: "FilterOptions",
                keyColumn: "Id",
                keyValue: 41,
                column: "Value",
                value: "Pošumavská");
        }
    }
}
