using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjouterReferentielDevises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Symbole = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TauxBaseDollar = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devises", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Devises",
                columns: new[] { "Id", "Code", "CreatedAt", "DateMiseAJour", "EstActive", "IsDeleted", "Libelle", "Symbole", "TauxBaseDollar", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "USD", new DateTime(2026, 5, 17, 15, 7, 53, 624, DateTimeKind.Utc).AddTicks(6098), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Dollar américain", "$", 1.0000m, null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "XOF", new DateTime(2026, 5, 17, 15, 7, 53, 646, DateTimeKind.Utc).AddTicks(4085), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Franc CFA (BCEAO)", "FCFA", 615.0000m, null },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "EUR", new DateTime(2026, 5, 17, 15, 7, 53, 646, DateTimeKind.Utc).AddTicks(4255), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Euro", "€", 0.9200m, null },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "GHS", new DateTime(2026, 5, 17, 15, 7, 53, 646, DateTimeKind.Utc).AddTicks(4272), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Cedi ghanéen", "₵", 14.5000m, null },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "NGN", new DateTime(2026, 5, 17, 15, 7, 53, 646, DateTimeKind.Utc).AddTicks(4280), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Naira nigérian", "₦", 1490.0000m, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devises_Code",
                table: "Devises",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Devises");
        }
    }
}
