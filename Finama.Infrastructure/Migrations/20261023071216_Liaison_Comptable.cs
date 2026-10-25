using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Liaison_Comptable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParametresComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LiaisonActive = table.Column<bool>(type: "bit", nullable: false),
                    ValiderAutomatiquement = table.Column<bool>(type: "bit", nullable: false),
                    ActiveDepuis = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompteVentes = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteAchats = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteTvaCollectee = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteTvaDeductible = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteClients = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteFournisseurs = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteBanque = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteCaisse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteMobileMoney = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteClientsCaisse = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CompteRemisesFidelite = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParametresComptables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParametresComptables_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PiecesComptabilisees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EcritureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateComptabilisation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PiecesComptabilisees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PiecesComptabilisees_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReglementsFacture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateReglement = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EcritureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReglementsFacture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReglementsFacture_Factures_FactureId",
                        column: x => x.FactureId,
                        principalTable: "Factures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ReglementsFacture_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 23, 7, 12, 14, 378, DateTimeKind.Utc).AddTicks(9183));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 23, 7, 12, 14, 400, DateTimeKind.Utc).AddTicks(6437));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 23, 7, 12, 14, 400, DateTimeKind.Utc).AddTicks(6600));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 23, 7, 12, 14, 400, DateTimeKind.Utc).AddTicks(6630));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 23, 7, 12, 14, 400, DateTimeKind.Utc).AddTicks(6639));

            migrationBuilder.CreateIndex(
                name: "IX_ParametresComptables_TenantId",
                table: "ParametresComptables",
                column: "TenantId",
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PiecesComptabilisees_TenantId_Source_SourceId",
                table: "PiecesComptabilisees",
                columns: new[] { "TenantId", "Source", "SourceId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReglementsFacture_FactureId",
                table: "ReglementsFacture",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_ReglementsFacture_TenantId",
                table: "ReglementsFacture",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParametresComptables");

            migrationBuilder.DropTable(
                name: "PiecesComptabilisees");

            migrationBuilder.DropTable(
                name: "ReglementsFacture");

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 21, 21, 40, 55, 878, DateTimeKind.Utc).AddTicks(5159));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 21, 21, 40, 55, 904, DateTimeKind.Utc).AddTicks(922));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 21, 21, 40, 55, 904, DateTimeKind.Utc).AddTicks(1405));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 21, 21, 40, 55, 904, DateTimeKind.Utc).AddTicks(1436));

            migrationBuilder.UpdateData(
                table: "Devises",
                keyColumn: "Id",
                keyValue: new Guid("55555555-5555-5555-5555-555555555555"),
                column: "CreatedAt",
                value: new DateTime(2026, 10, 21, 21, 40, 55, 904, DateTimeKind.Utc).AddTicks(1455));
        }
    }
}
