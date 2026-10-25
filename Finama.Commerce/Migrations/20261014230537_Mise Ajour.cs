using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class MiseAjour : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Activite",
                table: "Parametres",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TauxTvaDefaut",
                table: "Parametres",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "UtiliseLivres",
                table: "Parametres",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UtiliseLots",
                table: "Parametres",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UtiliseVrac",
                table: "Parametres",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ChampsJson",
                table: "Categories",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Activite",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "TauxTvaDefaut",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "UtiliseLivres",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "UtiliseLots",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "UtiliseVrac",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ChampsJson",
                table: "Categories");
        }
    }
}
