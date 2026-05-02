using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Init : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SlugUnique = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    NINEA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Adresse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeviseBase = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    PlanComptableCode = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Plan = table.Column<int>(type: "int", nullable: false),
                    AbonnementExpireAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompteComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Classe = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstSysteme = table.Column<bool>(type: "bit", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CompteParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompteComptables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompteComptables_CompteComptables_CompteParentId",
                        column: x => x.CompteParentId,
                        principalTable: "CompteComptables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CompteComptables_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Exercices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Annee = table.Column<int>(type: "int", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateFin = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstCloture = table.Column<bool>(type: "bit", nullable: false),
                    ClotureLe = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Exercices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Exercices_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Utilisateurs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MotDePasseHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    DerniereConnexionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RefreshToken = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RefreshTokenExpireAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateurs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Utilisateurs_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Tiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    NINEA = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Adresse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Devise = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CompteComptableId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tiers_CompteComptables_CompteComptableId",
                        column: x => x.CompteComptableId,
                        principalTable: "CompteComptables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Tiers_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Factures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateFacture = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    TiersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Devise = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TotalHT = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTVA = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTTC = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontantRegle = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Factures_Tiers_TiersId",
                        column: x => x.TiersId,
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Ecritures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DateEcriture = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Journal = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    ExerciceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactureId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ecritures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ecritures_Exercices_ExerciceId",
                        column: x => x.ExerciceId,
                        principalTable: "Exercices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ecritures_Factures_FactureId",
                        column: x => x.FactureId,
                        principalTable: "Factures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ecritures_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Ecritures_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LignesFacture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FactureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Quantite = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    PrixUnitaireHT = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TauxTVA = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    CompteProduitsId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesFacture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesFacture_CompteComptables_CompteProduitsId",
                        column: x => x.CompteProduitsId,
                        principalTable: "CompteComptables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LignesFacture_Factures_FactureId",
                        column: x => x.FactureId,
                        principalTable: "Factures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LignesFacture_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "LignesEcriture",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EcritureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CompteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TiersId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Debit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Devise = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    TauxChange = table.Column<decimal>(type: "decimal(18,6)", nullable: true),
                    MontantDeviseBase = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LignesEcriture", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LignesEcriture_CompteComptables_CompteId",
                        column: x => x.CompteId,
                        principalTable: "CompteComptables",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LignesEcriture_Ecritures_EcritureId",
                        column: x => x.EcritureId,
                        principalTable: "Ecritures",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LignesEcriture_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_LignesEcriture_Tiers_TiersId",
                        column: x => x.TiersId,
                        principalTable: "Tiers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_CompteComptables_CompteParentId",
                table: "CompteComptables",
                column: "CompteParentId");

            migrationBuilder.CreateIndex(
                name: "IX_CompteComptables_TenantId_Numero",
                table: "CompteComptables",
                columns: new[] { "TenantId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecritures_ExerciceId",
                table: "Ecritures",
                column: "ExerciceId");

            migrationBuilder.CreateIndex(
                name: "IX_Ecritures_FactureId",
                table: "Ecritures",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_Ecritures_TenantId_Reference",
                table: "Ecritures",
                columns: new[] { "TenantId", "Reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ecritures_UtilisateurId",
                table: "Ecritures",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Exercices_TenantId",
                table: "Exercices",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Factures_TenantId_Numero",
                table: "Factures",
                columns: new[] { "TenantId", "Numero" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Factures_TiersId",
                table: "Factures",
                column: "TiersId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesEcriture_CompteId",
                table: "LignesEcriture",
                column: "CompteId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesEcriture_EcritureId",
                table: "LignesEcriture",
                column: "EcritureId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesEcriture_TenantId",
                table: "LignesEcriture",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesEcriture_TiersId",
                table: "LignesEcriture",
                column: "TiersId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesFacture_CompteProduitsId",
                table: "LignesFacture",
                column: "CompteProduitsId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesFacture_FactureId",
                table: "LignesFacture",
                column: "FactureId");

            migrationBuilder.CreateIndex(
                name: "IX_LignesFacture_TenantId",
                table: "LignesFacture",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_SlugUnique",
                table: "Tenants",
                column: "SlugUnique",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_CompteComptableId",
                table: "Tiers",
                column: "CompteComptableId");

            migrationBuilder.CreateIndex(
                name: "IX_Tiers_TenantId",
                table: "Tiers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_TenantId_Email",
                table: "Utilisateurs",
                columns: new[] { "TenantId", "Email" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LignesEcriture");

            migrationBuilder.DropTable(
                name: "LignesFacture");

            migrationBuilder.DropTable(
                name: "Ecritures");

            migrationBuilder.DropTable(
                name: "Exercices");

            migrationBuilder.DropTable(
                name: "Factures");

            migrationBuilder.DropTable(
                name: "Utilisateurs");

            migrationBuilder.DropTable(
                name: "Tiers");

            migrationBuilder.DropTable(
                name: "CompteComptables");

            migrationBuilder.DropTable(
                name: "Tenants");
        }
    }
}
