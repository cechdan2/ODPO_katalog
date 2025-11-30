using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PhotoApp.Migrations
{
    /// <inheritdoc />
    public partial class AddFilterOptionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FilterOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FilterOptions", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "FilterOptions",
                columns: new[] { "Id", "Category", "SortOrder", "Value" },
                values: new object[,]
                {
                    { 1, "supplier", 0, "Oprava" },
                    { 2, "supplier", 0, "AA Group" },
                    { 3, "supplier", 0, "AGC" },
                    { 4, "supplier", 0, "Agor" },
                    { 5, "supplier", 0, "Archeo" },
                    { 6, "supplier", 0, "Vážeme" },
                    { 7, "supplier", 0, "Badico" },
                    { 8, "supplier", 0, "BBH" },
                    { 9, "supplier", 0, "Delasitas" },
                    { 10, "supplier", 0, "Dijmex" },
                    { 11, "supplier", 0, "Duo Pet" },
                    { 12, "supplier", 0, "JMK" },
                    { 13, "supplier", 0, "Ecoprimus" },
                    { 14, "supplier", 0, "EF Recycling" },
                    { 15, "supplier", 0, "Eri-trade" },
                    { 16, "supplier", 0, "Rumpold" },
                    { 17, "supplier", 0, "Fatra" },
                    { 18, "supplier", 0, "Gabeo" },
                    { 19, "supplier", 0, "GID" },
                    { 20, "supplier", 0, "Neveon" },
                    { 21, "supplier", 0, "Gumotex" },
                    { 22, "supplier", 0, "GZR" },
                    { 23, "supplier", 0, "Chintex" },
                    { 24, "supplier", 0, "Inno Comp" },
                    { 25, "supplier", 0, "Repla" },
                    { 26, "supplier", 0, "Juta" },
                    { 27, "supplier", 0, "Kamiddos" },
                    { 28, "supplier", 0, "Kantořík" },
                    { 29, "supplier", 0, "Kužílek" },
                    { 30, "supplier", 0, "KV Ekoplast" },
                    { 31, "supplier", 0, "Laszlo" },
                    { 32, "supplier", 0, "Leifheit" },
                    { 33, "supplier", 0, "Magna" },
                    { 34, "supplier", 0, "Mondeco" },
                    { 35, "supplier", 0, "Nexis" },
                    { 36, "supplier", 0, "Oceanize" },
                    { 37, "supplier", 0, "odpo" },
                    { 38, "supplier", 0, "Power-Full" },
                    { 39, "supplier", 0, "PFN" },
                    { 40, "supplier", 0, "PlastMetal" },
                    { 41, "supplier", 0, "Pošumavská" },
                    { 42, "supplier", 0, "Prodos rec" },
                    { 43, "supplier", 0, "Rapol" },
                    { 44, "supplier", 0, "Regoplast" },
                    { 45, "supplier", 0, "Remaq" },
                    { 46, "supplier", 0, "Renoplasti" },
                    { 47, "supplier", 0, "Reyond" },
                    { 48, "supplier", 0, "Silon Recy" },
                    { 49, "supplier", 0, "Suchan" },
                    { 50, "supplier", 0, "TKC Kunst" },
                    { 51, "supplier", 0, "Torray" },
                    { 52, "supplier", 0, "Valek" },
                    { 53, "supplier", 0, "Vansida" },
                    { 54, "supplier", 0, "Witt and M" },
                    { 55, "supplier", 0, "Witte" },
                    { 56, "supplier", 0, "Zeba" },
                    { 57, "supplier", 0, "ZMPB" },
                    { 58, "form", 0, "Form" },
                    { 59, "form", 0, "Regrind" },
                    { 60, "form", 0, "Scrap" },
                    { 61, "form", 0, "Regranulate" },
                    { 62, "form", 0, "Ingots" },
                    { 63, "form", 0, "Pellets" },
                    { 64, "form", 0, "Yarn" },
                    { 65, "form", 0, "Bales" },
                    { 66, "form", 0, "Lumps" },
                    { 67, "form", 0, "Rolls" },
                    { 68, "form", 0, "Other" },
                    { 69, "form", 0, "Virgin" },
                    { 70, "filler", 0, "Filler" },
                    { 71, "filler", 0, "GF" },
                    { 72, "filler", 0, "TD" },
                    { 73, "filler", 0, "MD" },
                    { 74, "filler", 0, "TV" },
                    { 75, "filler", 0, "CF" },
                    { 76, "filler", 0, "LGF" },
                    { 77, "filler", 0, "ESD" },
                    { 78, "color", 0, "Colour" },
                    { 79, "color", 0, "Red" },
                    { 80, "color", 0, "Black" },
                    { 81, "color", 0, "Blue" },
                    { 82, "color", 0, "Grey" },
                    { 83, "material", 0, "PP" },
                    { 84, "material", 0, "LDPE" },
                    { 85, "material", 0, "HDPE" },
                    { 86, "material", 0, "PC/ABS" },
                    { 87, "material", 0, "PA6" },
                    { 88, "material", 0, "PA66" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FilterOptions");
        }
    }
}
