using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finama.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Inot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devises",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Symbole = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TauxBaseDollar = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DateMiseAJour = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devises", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pays",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CodeISO = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    DeviseCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DeviseSymbole = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    TauxTVAStandard = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CodeFiscal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Langue = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SlugUnique = table.Column<string>(type: "text", nullable: false),
                    NumeroFiscal = table.Column<string>(type: "text", nullable: true),
                    Adresse = table.Column<string>(type: "text", nullable: true),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PaysId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviseBase = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TauxTVA = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    AssujettTVA = table.Column<bool>(type: "boolean", nullable: false),
                    PlanComptableCode = table.Column<string>(type: "text", nullable: false),
                    Plan = table.Column<int>(type: "integer", nullable: false),
                    AbonnementExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_Pays_PaysId",
                        column: x => x.PaysId,
                        principalTable: "Pays",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ClassesComptables",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<int>(type: "integer", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassesComptables", x => new { x.TenantId, x.Numero });
                    table.ForeignKey(
                        name: "FK_ClassesComptables_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CompteComptables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Libelle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Classe = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    EstSysteme = table.Column<bool>(type: "boolean", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CompteParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Annee = table.Column<int>(type: "integer", nullable: false),
                    DateDebut = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateFin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstCloture = table.Column<bool>(type: "boolean", nullable: false),
                    ClotureLe = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Prenom = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MotDePasseHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    DerniereConnexionAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefreshToken = table.Column<string>(type: "text", nullable: true),
                    RefreshTokenExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Nom = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    NINEA = table.Column<string>(type: "text", nullable: true),
                    Adresse = table.Column<string>(type: "text", nullable: true),
                    Telephone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Devise = table.Column<string>(type: "text", nullable: true),
                    CompteComptableId = table.Column<Guid>(type: "uuid", nullable: true),
                    EstActif = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Numero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    DateFacture = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateEcheance = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    TiersId = table.Column<Guid>(type: "uuid", nullable: false),
                    Devise = table.Column<string>(type: "text", nullable: false),
                    TotalHT = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTVA = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TotalTTC = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    MontantRegle = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    DateEcriture = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Libelle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Journal = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    Statut = table.Column<int>(type: "integer", nullable: false),
                    ExerciceId = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: true),
                    UtilisateurId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FactureId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Quantite = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PrixUnitaireHT = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    TauxTVA = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CompteProduitsId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EcritureId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompteId = table.Column<Guid>(type: "uuid", nullable: false),
                    TiersId = table.Column<Guid>(type: "uuid", nullable: true),
                    Libelle = table.Column<string>(type: "text", nullable: true),
                    Debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Devise = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TauxChange = table.Column<decimal>(type: "numeric(18,6)", nullable: true),
                    MontantDeviseBase = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
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

            migrationBuilder.InsertData(
                table: "Devises",
                columns: new[] { "Id", "Code", "CreatedAt", "DateMiseAJour", "EstActive", "IsDeleted", "Libelle", "Symbole", "TauxBaseDollar", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "USD", new DateTime(2026, 5, 17, 23, 23, 9, 5, DateTimeKind.Utc).AddTicks(5792), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Dollar américain", "$", 1.0000m, null },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "XOF", new DateTime(2026, 5, 17, 23, 23, 9, 19, DateTimeKind.Utc).AddTicks(4720), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Franc CFA (BCEAO)", "FCFA", 615.0000m, null },
                    { new Guid("33333333-3333-3333-3333-333333333333"), "EUR", new DateTime(2026, 5, 17, 23, 23, 9, 19, DateTimeKind.Utc).AddTicks(4906), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Euro", "€", 0.9200m, null },
                    { new Guid("44444444-4444-4444-4444-444444444444"), "GHS", new DateTime(2026, 5, 17, 23, 23, 9, 19, DateTimeKind.Utc).AddTicks(4927), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Cedi ghanéen", "₵", 14.5000m, null },
                    { new Guid("55555555-5555-5555-5555-555555555555"), "NGN", new DateTime(2026, 5, 17, 23, 23, 9, 19, DateTimeKind.Utc).AddTicks(4938), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Local), true, false, "Naira nigérian", "₦", 1490.0000m, null }
                });

            migrationBuilder.InsertData(
                table: "Pays",
                columns: new[] { "Id", "CodeFiscal", "CodeISO", "CreatedAt", "DeviseCode", "DeviseSymbole", "EstActif", "IsDeleted", "Langue", "Nom", "TauxTVAStandard", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "NIF", "TG", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XOF", "FCFA", true, false, "fr", "Togo", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "NIF", "ST", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "STD", "Db", true, false, "pt", "São Tomé et Príncipe", 15m, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "NINEA", "SN", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XOF", "FCFA", true, false, "fr", "Sénégal", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "DGI", "CI", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XOF", "FCFA", true, false, "fr", "Côte d'Ivoire", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "IFU", "BJ", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XOF", "FCFA", true, false, "fr", "Bénin", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "IFU", "BF", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XOF", "FCFA", true, false, "fr", "Burkina Faso", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "NIF", "ML", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XOF", "FCFA", true, false, "fr", "Mali", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "NIF", "NE", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XOF", "FCFA", true, false, "fr", "Niger", 19m, null },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "NIU", "CM", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XAF", "FCFA", true, false, "fr", "Cameroun", 19.25m, null },
                    { new Guid("10000000-0000-0000-0000-000000000010"), "NIF", "GA", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XAF", "FCFA", true, false, "fr", "Gabon", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000011"), "NIU", "CG", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "XAF", "FCFA", true, false, "fr", "Congo", 18m, null },
                    { new Guid("10000000-0000-0000-0000-000000000012"), "NIF", "GN", new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "GNF", "FG", true, false, "fr", "Guinée", 18m, null }
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
                name: "IX_Devises_Code",
                table: "Devises",
                column: "Code",
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
                name: "IX_Pays_CodeISO",
                table: "Pays",
                column: "CodeISO",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pays_DeviseCode",
                table: "Pays",
                column: "DeviseCode");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PaysId",
                table: "Tenants",
                column: "PaysId");

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
                name: "ClassesComptables");

            migrationBuilder.DropTable(
                name: "Devises");

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

            migrationBuilder.DropTable(
                name: "Pays");
        }
    }
}
