using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class Miseajou : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AttributsJson",
                table: "Produits",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MontantParPoint",
                table: "Parametres",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ValeurPointFcfa",
                table: "Parametres",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Adresse = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Nif = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    TiersId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PlafondCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SoldeCredit = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PointsFidelite = table.Column<int>(type: "int", nullable: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateDerniereVente = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MouvementsFidelite",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<int>(type: "int", nullable: false),
                    SoldeApres = table.Column<int>(type: "int", nullable: false),
                    Motif = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateMouvement = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MouvementsFidelite", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SequencesDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cle = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    DernierNumero = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SequencesDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId_Code",
                table: "Clients",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Clients_TenantId_Telephone",
                table: "Clients",
                columns: new[] { "TenantId", "Telephone" });

            migrationBuilder.CreateIndex(
                name: "IX_MouvementsFidelite_TenantId_ClientId_DateMouvement",
                table: "MouvementsFidelite",
                columns: new[] { "TenantId", "ClientId", "DateMouvement" });

            migrationBuilder.CreateIndex(
                name: "IX_SequencesDocuments_TenantId_Cle_Annee",
                table: "SequencesDocuments",
                columns: new[] { "TenantId", "Cle", "Annee" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "MouvementsFidelite");

            migrationBuilder.DropTable(
                name: "SequencesDocuments");

            migrationBuilder.DropColumn(
                name: "AttributsJson",
                table: "Produits");

            migrationBuilder.DropColumn(
                name: "MontantParPoint",
                table: "Parametres");

            migrationBuilder.DropColumn(
                name: "ValeurPointFcfa",
                table: "Parametres");
        }
    }
}
