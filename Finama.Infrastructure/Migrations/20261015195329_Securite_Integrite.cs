using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Securite_Integrite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Exercices_TenantId",
                table: "Exercices");

            migrationBuilder.AddColumn<int>(
                name: "OtpTentatives",
                table: "Utilisateurs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SequencesComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cle = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    DernierNumero = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequencesComptables", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 15, 19, 53, 26, 885, DateTimeKind.Utc).AddTicks(8731));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 15, 19, 53, 26, 909, DateTimeKind.Utc).AddTicks(881));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 15, 19, 53, 26, 909, DateTimeKind.Utc).AddTicks(994));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 15, 19, 53, 26, 909, DateTimeKind.Utc).AddTicks(1016));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 15, 19, 53, 26, 909, DateTimeKind.Utc).AddTicks(1025));

            migrationBuilder.CreateIndex(
                name: "IX_Exercices_TenantId_Annee",
                table: "Exercices",
                columns: new[] { "TenantId", "Annee" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SequencesComptables_TenantId_Cle_Annee",
                table: "SequencesComptables",
                columns: new[] { "TenantId", "Cle", "Annee" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SequencesComptables");

            migrationBuilder.DropIndex(
                name: "IX_Exercices_TenantId_Annee",
                table: "Exercices");

            migrationBuilder.DropColumn(
                name: "OtpTentatives",
                table: "Utilisateurs");

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 136, DateTimeKind.Utc).AddTicks(8619));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6370));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6750));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6769));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 12, 13, 48, 19, 170, DateTimeKind.Utc).AddTicks(6797));

            migrationBuilder.CreateIndex(
                name: "IX_Exercices_TenantId",
                table: "Exercices",
                column: "TenantId");
        }
    }
}
