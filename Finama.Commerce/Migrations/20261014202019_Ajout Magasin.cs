using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class AjoutMagasin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Responsable",
                table: "Magasins",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Telephone",
                table: "Magasins",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Magasins",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Responsable",
                table: "Magasins");

            migrationBuilder.DropColumn(
                name: "Telephone",
                table: "Magasins");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Magasins");
        }
    }
}
