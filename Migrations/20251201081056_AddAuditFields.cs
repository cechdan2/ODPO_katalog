using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhotoApp.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "Photos",
                type: "TEXT",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "Photos",
                type: "TEXT",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Photos");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "Photos");
        }
    }
}
