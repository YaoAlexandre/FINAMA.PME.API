using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AjoutDemandeAcces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DemandesAcces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomEntreprise = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NomContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrenomContact = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PaysId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    DateDemande = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateTraitement = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotifRefus = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DemandesAcces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DemandesAcces_Pays_PaysId",
                        column: x => x.PaysId,
                        principalTable: "Pays",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 39, DateTimeKind.Utc).AddTicks(5690));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6284));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6419));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6432));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 4, 14, 46, 52, 59, DateTimeKind.Utc).AddTicks(6440));

            migrationBuilder.CreateIndex(
                name: "IX_DemandesAcces_PaysId",
                table: "DemandesAcces",
                column: "PaysId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DemandesAcces");

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 9, 5, 13, 59, 20, 786, DateTimeKind.Utc).AddTicks(7032));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 9, 5, 13, 59, 20, 805, DateTimeKind.Utc).AddTicks(716));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 9, 5, 13, 59, 20, 805, DateTimeKind.Utc).AddTicks(885));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 9, 5, 13, 59, 20, 805, DateTimeKind.Utc).AddTicks(898));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 9, 5, 13, 59, 20, 805, DateTimeKind.Utc).AddTicks(906));
        }
    }
}
