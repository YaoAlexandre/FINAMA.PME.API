using Finama.Core.Entities;

namespace Finama.Infrastructure.Seeds;

/// <summary>
/// Données initiales des pays OHADA.
/// Injectées via migration — modifiables ensuite depuis le panel admin
/// sans toucher au code source.
/// </summary>
public static class PaysSeed
{
    public static readonly List<PaysConfig> Pays =
    [
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000001"),
            Nom            = "Togo",
            CodeISO        = "TG",
            DeviseCode     = "XOF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "NIF",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000002"),
            Nom            = "São Tomé et Príncipe",
            CodeISO        = "ST",
            DeviseCode     = "STD",
            DeviseSymbole  = "Db",
            TauxTVAStandard= 15m,
            CodeFiscal     = "NIF",
            Langue         = "pt",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000003"),
            Nom            = "Sénégal",
            CodeISO        = "SN",
            DeviseCode     = "XOF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "NINEA",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000004"),
            Nom            = "Côte d'Ivoire",
            CodeISO        = "CI",
            DeviseCode     = "XOF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "DGI",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000005"),
            Nom            = "Bénin",
            CodeISO        = "BJ",
            DeviseCode     = "XOF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "IFU",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000006"),
            Nom            = "Burkina Faso",
            CodeISO        = "BF",
            DeviseCode     = "XOF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "IFU",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000007"),
            Nom            = "Mali",
            CodeISO        = "ML",
            DeviseCode     = "XOF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "NIF",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000008"),
            Nom            = "Niger",
            CodeISO        = "NE",
            DeviseCode     = "XOF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 19m,
            CodeFiscal     = "NIF",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000009"),
            Nom            = "Cameroun",
            CodeISO        = "CM",
            DeviseCode     = "XAF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 19.25m,
            CodeFiscal     = "NIU",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000010"),
            Nom            = "Gabon",
            CodeISO        = "GA",
            DeviseCode     = "XAF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "NIF",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000011"),
            Nom            = "Congo",
            CodeISO        = "CG",
            DeviseCode     = "XAF",
            DeviseSymbole  = "FCFA",
            TauxTVAStandard= 18m,
            CodeFiscal     = "NIU",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
        new PaysConfig
        {
            Id             = new Guid("10000000-0000-0000-0000-000000000012"),
            Nom            = "Guinée",
            CodeISO        = "GN",
            DeviseCode     = "GNF",
            DeviseSymbole  = "FG",
            TauxTVAStandard= 18m,
            CodeFiscal     = "NIF",
            Langue         = "fr",
            EstActif       = true,
            CreatedAt      = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        },
    ];
}
