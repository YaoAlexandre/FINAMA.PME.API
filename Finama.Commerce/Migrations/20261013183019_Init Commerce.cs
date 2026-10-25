using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finama.Commerce.Migrations
{
    /// <inheritdoc />
    public partial class InitCommerce : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Caisses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MagasinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Caisses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Categories_Categories_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Editeurs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Contact = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Editeurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Inventaires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MagasinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    DateOuverture = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateCloture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClotureParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Inventaires", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MagasinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroLot = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DatePeremption = table.Column<DateOnly>(type: "date", nullable: true),
                    DateReception = table.Column<DateOnly>(type: "date", nullable: false),
                    QuantiteInitiale = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantiteRestante = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    CoutUnitaire = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Magasins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Adresse = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Magasins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Mouvements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MagasinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Quantite = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StockApres = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CoutUnitaire = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Motif = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateMouvement = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mouvements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parametres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SeuilRemiseSansPinPourcent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RemiseMaxPourcent = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SeuilEcartCaisseSignale = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PrefixeTicket = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AutoriserStockNegatif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parametres", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaisseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MagasinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaissierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateOuverture = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FondDeCaisse = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    DateCloture = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StocksProduits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MagasinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantite = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    DateDernierMouvement = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StocksProduits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SuperviseurPins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PinHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    TentativesEchouees = table.Column<int>(type: "int", nullable: false),
                    VerrouJusqua = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuperviseurPins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Ventes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MagasinId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaisseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionCaisseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaissierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroTicket = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateVente = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Statut = table.Column<int>(type: "int", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TotalHT = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTVA = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalRemise = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalTTC = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontantPaye = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RenduMonnaie = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RemiseAutoriseeParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MotifAnnulation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnnuleParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateAnnulation = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ventes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Produits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Unite = table.Column<int>(type: "int", nullable: false),
                    CategorieId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PrixVenteTTC = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TauxTva = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    PrixAchatMoyen = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    DernierPrixAchat = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    SuiviLots = table.Column<bool>(type: "bit", nullable: false),
                    JoursAlertePeremption = table.Column<int>(type: "int", nullable: false),
                    SeuilReappro = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    QuantiteReappro = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Produits_Categories_CategorieId",
                        column: x => x.CategorieId,
                        principalTable: "Categories",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventaireLignes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InventaireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LotId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    QuantiteTheorique = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    QuantiteComptee = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventaireLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventaireLignes_Inventaires_InventaireId",
                        column: x => x.InventaireId,
                        principalTable: "Inventaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Clotures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionCaisseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateCloture = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalDeclare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalTheorique = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EcartTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EcartSignale = table.Column<bool>(type: "bit", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValideParId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DateValidation = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clotures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Clotures_Sessions_SessionCaisseId",
                        column: x => x.SessionCaisseId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Paiements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionCaisseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    Montant = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontantRecu = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Operateur = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DatePaiement = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Paiements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Paiements_Ventes_VenteId",
                        column: x => x.VenteId,
                        principalTable: "Ventes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VenteLignes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CodeBarres = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantite = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PrixUnitaireTTC = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RemisePourcent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RemiseMontant = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TauxTva = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    MontantHT = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontantTVA = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MontantTTC = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CoutUnitaire = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VenteLignes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VenteLignes_Ventes_VenteId",
                        column: x => x.VenteId,
                        principalTable: "Ventes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CodesBarres",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EstPrincipal = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodesBarres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodesBarres_Produits_ProduitId",
                        column: x => x.ProduitId,
                        principalTable: "Produits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProduitsLivres",
                columns: table => new
                {
                    ProduitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Isbn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Auteur = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EditeurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Collection = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AnneeEdition = table.Column<int>(type: "int", nullable: true),
                    Rayon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Etagere = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetournableEditeur = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProduitsLivres", x => x.ProduitId);
                    table.ForeignKey(
                        name: "FK_ProduitsLivres_Editeurs_EditeurId",
                        column: x => x.EditeurId,
                        principalTable: "Editeurs",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ProduitsLivres_Produits_ProduitId",
                        column: x => x.ProduitId,
                        principalTable: "Produits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClotureCaisseLigne",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClotureCaisseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    MontantDeclare = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    MontantTheorique = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Ecart = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClotureCaisseLigne", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClotureCaisseLigne_Clotures_ClotureCaisseId",
                        column: x => x.ClotureCaisseId,
                        principalTable: "Clotures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_ClotureCaisseLigne_ClotureCaisseId",
                table: "ClotureCaisseLigne",
                column: "ClotureCaisseId");

            migrationBuilder.CreateIndex(
                name: "IX_Clotures_SessionCaisseId",
                table: "Clotures",
                column: "SessionCaisseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodesBarres_ProduitId",
                table: "CodesBarres",
                column: "ProduitId");

            migrationBuilder.CreateIndex(
                name: "IX_CodesBarres_TenantId_Code",
                table: "CodesBarres",
                columns: new[] { "TenantId", "Code" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InventaireLignes_InventaireId",
                table: "InventaireLignes",
                column: "InventaireId");

            migrationBuilder.CreateIndex(
                name: "IX_Lots_TenantId_MagasinId_ProduitId_NumeroLot",
                table: "Lots",
                columns: new[] { "TenantId", "MagasinId", "ProduitId", "NumeroLot" });

            migrationBuilder.CreateIndex(
                name: "IX_Paiements_VenteId",
                table: "Paiements",
                column: "VenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Produits_CategorieId",
                table: "Produits",
                column: "CategorieId");

            migrationBuilder.CreateIndex(
                name: "IX_Produits_TenantId_Reference",
                table: "Produits",
                columns: new[] { "TenantId", "Reference" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ProduitsLivres_EditeurId",
                table: "ProduitsLivres",
                column: "EditeurId");

            migrationBuilder.CreateIndex(
                name: "IX_StocksProduits_TenantId_MagasinId_ProduitId",
                table: "StocksProduits",
                columns: new[] { "TenantId", "MagasinId", "ProduitId" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_VenteLignes_VenteId",
                table: "VenteLignes",
                column: "VenteId");

            migrationBuilder.CreateIndex(
                name: "IX_Ventes_TenantId_DateVente",
                table: "Ventes",
                columns: new[] { "TenantId", "DateVente" });

            migrationBuilder.CreateIndex(
                name: "IX_Ventes_TenantId_NumeroTicket",
                table: "Ventes",
                columns: new[] { "TenantId", "NumeroTicket" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Caisses");

            migrationBuilder.DropTable(
                name: "ClotureCaisseLigne");

            migrationBuilder.DropTable(
                name: "CodesBarres");

            migrationBuilder.DropTable(
                name: "InventaireLignes");

            migrationBuilder.DropTable(
                name: "Lots");

            migrationBuilder.DropTable(
                name: "Magasins");

            migrationBuilder.DropTable(
                name: "Mouvements");

            migrationBuilder.DropTable(
                name: "Paiements");

            migrationBuilder.DropTable(
                name: "Parametres");

            migrationBuilder.DropTable(
                name: "ProduitsLivres");

            migrationBuilder.DropTable(
                name: "StocksProduits");

            migrationBuilder.DropTable(
                name: "SuperviseurPins");

            migrationBuilder.DropTable(
                name: "VenteLignes");

            migrationBuilder.DropTable(
                name: "Clotures");

            migrationBuilder.DropTable(
                name: "Inventaires");

            migrationBuilder.DropTable(
                name: "Editeurs");

            migrationBuilder.DropTable(
                name: "Produits");

            migrationBuilder.DropTable(
                name: "Ventes");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Categories");
        }
    }
}
