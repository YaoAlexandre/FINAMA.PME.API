using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class Vente_HorsLigne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EstHorsLigne",
                table: "Ventes",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NumeroHorsLigne",
                table: "Ventes",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EstHorsLigne",
                table: "Ventes");

            migrationBuilder.DropColumn(
                name: "NumeroHorsLigne",
                table: "Ventes");
        }
    }
}
