using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoApp.Migrations
{
    /// <inheritdoc />
    public partial class FixAdditionalPhotosColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
        -- Přidá sloupec, jen pokud neexistuje
        ALTER TABLE Photos ADD COLUMN AdditionalPhotos TEXT;
    ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalPhotos",
                table: "Photos");
        }
    }
}
