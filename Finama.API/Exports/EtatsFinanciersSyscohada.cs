using Finama.Core.DTOs;

namespace Finama.API.Exports;

/// <summary>
/// Bilan et compte de résultat au format SYSCOHADA révisé (système normal, présentation par rubriques),
/// calculés à partir des soldes de la balance par préfixe de compte.
/// Règle des comptes de tiers et de trésorerie : solde débiteur à l'actif, solde créditeur au passif, compte par compte.
/// À faire valider par l'expert-comptable avant dépôt de la liasse : c'est une aide à la production, pas la DSF officielle.
/// </summary>
public static class EtatsFinanciersSyscohada
{
    public sealed record LigneActif(string Ref, string Libelle, decimal Brut, decimal AmortDeprec)
    {
        public decimal Net => Brut - AmortDeprec;
    }

    public sealed record LignePassif(string Ref, string Libelle, decimal Montant);

    public sealed record LigneResultat(string Ref, string Libelle, decimal Montant, bool EstSolde);

    // ─────────────────────────────────────────────────────────────────────────
    // BILAN — ACTIF
    // ─────────────────────────────────────────────────────────────────────────
    public static List<(string Section, List<LigneActif> Lignes)> Actif(IReadOnlyList<LigneBalanceDto> balance)
    {
        decimal Brut(params string[] p) => SoldeDebiteur(balance, p);
        decimal Amort(params string[] p) => SoldeCrediteur(balance, p);

        var immobilise = new List<LigneActif>
        {
            new("AD", "Immobilisations incorporelles", Brut("21"), Amort("281", "291")),
            new("AI", "Immobilisations corporelles", Brut("22", "23", "24"), Amort("282", "283", "284", "292", "293", "294")),
            new("AP", "Avances et acomptes versés sur immobilisations", Brut("25"), Amort("295")),
            new("AQ", "Immobilisations financières", Brut("26", "27"), Amort("296", "297")),
        };

        var circulant = new List<LigneActif>
        {
            new("BA", "Actif circulant HAO", Brut("485", "488"), Amort("498")),
            new("BB", "Stocks et encours", Brut("31", "32", "33", "34", "35", "36", "37", "38"), Amort("39")),
            new("BH", "Fournisseurs, avances versées", DebiteursTiers(balance, "409"), 0),
            new("BI", "Clients", DebiteursTiers(balance, "41"), Amort("491")),
            new("BJ", "Autres créances", DebiteursTiers(balance, "42", "43", "44", "45", "46", "47"), Amort("492", "493", "494", "495", "496", "497", "499")),
        };

        var tresorerie = new List<LigneActif>
        {
            new("BQ", "Titres de placement", Brut("50"), Amort("590")),
            new("BR", "Valeurs à encaisser", Brut("51"), Amort("591")),
            new("BS", "Banques, chèques postaux, caisse et assimilés", DebiteursTiers(balance, "52", "53", "54", "55", "57", "58"), Amort("592", "593", "594")),
        };

        var ecart = new List<LigneActif>
        {
            new("BU", "Écart de conversion-Actif", Brut("478"), 0),
        };

        return
        [
            ("ACTIF IMMOBILISÉ", immobilise),
            ("ACTIF CIRCULANT", circulant),
            ("TRÉSORERIE-ACTIF", tresorerie),
            ("ÉCART DE CONVERSION", ecart),
        ];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BILAN — PASSIF (résultat de l'exercice intégré : comptes 6/7 non encore soldés)
    // ─────────────────────────────────────────────────────────────────────────
    public static List<(string Section, List<LignePassif> Lignes)> Passif(IReadOnlyList<LigneBalanceDto> balance)
    {
        decimal Credit(params string[] p) => SoldeNetCrediteur(balance, p);

        // Résultat : solde du 13 (exercice clôturé ou résultat antérieur non affecté)
        // + résultat en cours des comptes de gestion encore ouverts (classes 6/7/8).
        var resultat = SoldeNetCrediteur(balance, "13") + ResultatNet(balance);

        var capitaux = new List<LignePassif>
        {
            new("CA", "Capital", Credit("101", "102", "103", "104", "108")),
            new("CB", "Apporteurs capital non appelé (−)", -SoldeDebiteur(balance, "109")),
            new("CD", "Primes liées au capital social", Credit("105")),
            new("CE", "Écarts de réévaluation", Credit("106")),
            new("CF", "Réserves indisponibles", Credit("111", "112", "113")),
            new("CG", "Réserves libres", Credit("118")),
            new("CH", "Report à nouveau (+ ou −)", Credit("12")),
            new("CJ", "Résultat net de l'exercice (bénéfice + ou perte −)", resultat),
            new("CL", "Subventions d'investissement", Credit("14")),
            new("CM", "Provisions réglementées", Credit("15")),
        };

        var dettesFinancieres = new List<LignePassif>
        {
            new("DA", "Emprunts et dettes financières diverses", Credit("16", "18")),
            new("DB", "Dettes de location-acquisition", Credit("17")),
            new("DC", "Provisions pour risques et charges", Credit("19")),
        };

        var passifCirculant = new List<LignePassif>
        {
            new("DH", "Dettes circulantes HAO", Comptes(balance, ["481", "482", "484"]).Sum(x => Math.Max(0, x.SoldeFinalCredit - x.SoldeFinalDebit))),
            new("DI", "Clients, avances reçues", CrediteursTiers(balance, "419")),
            new("DJ", "Fournisseurs d'exploitation", CrediteursTiers(balance, "40")),
            new("DK", "Dettes fiscales et sociales", CrediteursTiers(balance, "42", "43", "44")),
            new("DM", "Autres dettes", CrediteursTiers(balance, "45", "46", "47")),
            new("DN", "Provisions pour risques à court terme", Credit("499")),
        };

        var tresoPassif = new List<LignePassif>
        {
            new("DQ", "Banques, crédits d'escompte", CrediteursTiers(balance, "565")),
            new("DR", "Banques, établissements financiers et crédits de trésorerie", CrediteursTiers(balance, "52", "53", "561", "564", "566")),
        };

        var ecart = new List<LignePassif>
        {
            new("DV", "Écart de conversion-Passif", Credit("479")),
        };

        return
        [
            ("CAPITAUX PROPRES ET RESSOURCES ASSIMILÉES", capitaux),
            ("DETTES FINANCIÈRES ET RESSOURCES ASSIMILÉES", dettesFinancieres),
            ("PASSIF CIRCULANT", passifCirculant),
            ("TRÉSORERIE-PASSIF", tresoPassif),
            ("ÉCART DE CONVERSION", ecart),
        ];
    }

    // ─────────────────────────────────────────────────────────────────────────
    // COMPTE DE RÉSULTAT (soldes intermédiaires de gestion SYSCOHADA)
    // Produits en positif, charges en négatif ; les lignes « solde » cumulent.
    // ─────────────────────────────────────────────────────────────────────────
    public static List<LigneResultat> CompteDeResultat(IReadOnlyList<LigneBalanceDto> balance)
    {
        decimal P(params string[] p) => SoldeNetCrediteur(balance, p);   // produit (+)
        decimal C(params string[] p) => -SoldeNetDebiteur(balance, p);   // charge (−)

        var l = new List<LigneResultat>();
        void Ajout(string r, string lib, decimal m) => l.Add(new(r, lib, m, false));
        void Solde(string r, string lib, decimal m) => l.Add(new(r, lib, m, true));

        var ta = P("701"); Ajout("TA", "Ventes de marchandises", ta);
        var ra = C("601"); Ajout("RA", "Achats de marchandises", ra);
        var rb = C("6031"); Ajout("RB", "Variation de stocks de marchandises", rb);
        var xa = ta + ra + rb; Solde("XA", "MARGE COMMERCIALE", xa);

        var tb = P("702", "703", "704"); Ajout("TB", "Ventes de produits fabriqués", tb);
        var tc = P("705", "706"); Ajout("TC", "Travaux, services vendus", tc);
        var td = P("707"); Ajout("TD", "Produits accessoires", td);
        var xb = ta + tb + tc + td; Solde("XB", "CHIFFRE D'AFFAIRES", xb);

        var te = P("73"); Ajout("TE", "Production stockée (ou déstockage)", te);
        var tf = P("72"); Ajout("TF", "Production immobilisée", tf);
        var tg = P("71"); Ajout("TG", "Subventions d'exploitation", tg);
        var th = P("75"); Ajout("TH", "Autres produits", th);
        var ti = P("781"); Ajout("TI", "Transferts de charges d'exploitation", ti);
        var rc = C("602"); Ajout("RC", "Achats de matières premières et fournitures liées", rc);
        var rd = C("6032"); Ajout("RD", "Variation de stocks de matières premières", rd);
        var re = C("604", "605", "608"); Ajout("RE", "Autres achats", re);
        var rf = C("6033"); Ajout("RF", "Variation de stocks d'autres approvisionnements", rf);
        var rg = C("61"); Ajout("RG", "Transports", rg);
        var rh = C("62", "63"); Ajout("RH", "Services extérieurs", rh);
        var ri = C("64"); Ajout("RI", "Impôts et taxes", ri);
        var rj = C("65"); Ajout("RJ", "Autres charges", rj);
        var xc = xb + ra + rb + te + tf + tg + th + ti + rc + rd + re + rf + rg + rh + ri + rj;
        Solde("XC", "VALEUR AJOUTÉE", xc);

        var rk = C("66"); Ajout("RK", "Charges de personnel", rk);
        var xd = xc + rk; Solde("XD", "EXCÉDENT BRUT D'EXPLOITATION", xd);

        var tj = P("791", "798", "799"); Ajout("TJ", "Reprises d'amortissements, provisions et dépréciations", tj);
        var rl = C("681", "691"); Ajout("RL", "Dotations aux amortissements, provisions et dépréciations", rl);
        var xe = xd + tj + rl; Solde("XE", "RÉSULTAT D'EXPLOITATION", xe);

        var tk = P("77"); Ajout("TK", "Revenus financiers et assimilés", tk);
        var tl = P("797"); Ajout("TL", "Reprises de provisions et dépréciations financières", tl);
        var tm = P("787"); Ajout("TM", "Transferts de charges financières", tm);
        var rm = C("67"); Ajout("RM", "Frais financiers et charges assimilées", rm);
        var rn = C("697"); Ajout("RN", "Dotations aux provisions et dépréciations financières", rn);
        var xf = tk + tl + tm + rm + rn; Solde("XF", "RÉSULTAT FINANCIER", xf);

        var xg = xe + xf; Solde("XG", "RÉSULTAT DES ACTIVITÉS ORDINAIRES", xg);

        var tn = P("82"); Ajout("TN", "Produits des cessions d'immobilisations", tn);
        var to = P("84", "86", "88"); Ajout("TO", "Autres produits HAO", to);
        var ro = C("81"); Ajout("RO", "Valeurs comptables des cessions d'immobilisations", ro);
        var rp = C("83", "85"); Ajout("RP", "Autres charges HAO", rp);
        var xh = tn + to + ro + rp; Solde("XH", "RÉSULTAT HORS ACTIVITÉS ORDINAIRES", xh);

        var rq = C("87"); Ajout("RQ", "Participation des travailleurs", rq);
        var rs = C("89"); Ajout("RS", "Impôts sur le résultat", rs);
        Solde("XI", "RÉSULTAT NET", xg + xh + rq + rs);

        return l;
    }

    /// <summary>Résultat de l'exercice non encore clôturé : produits (7, 8 créditeurs) − charges (6, 8 débiteurs).</summary>
    public static decimal ResultatNet(IReadOnlyList<LigneBalanceDto> balance)
        => balance.Where(x => x.Classe is 6 or 7 or 8).Sum(x => x.SoldeFinalCredit - x.SoldeFinalDebit);

    // ─── Agrégation par préfixe ──────────────────────────────────────────────

    private static IEnumerable<LigneBalanceDto> Comptes(IReadOnlyList<LigneBalanceDto> b, string[] prefixes)
        => b.Where(x => prefixes.Any(p => x.Numero.StartsWith(p, StringComparison.Ordinal)));

    /// <summary>Solde net débiteur (débit − crédit) d'un groupe de comptes.</summary>
    private static decimal SoldeNetDebiteur(IReadOnlyList<LigneBalanceDto> b, params string[] p)
        => Comptes(b, p).Sum(x => x.SoldeFinalDebit - x.SoldeFinalCredit);

    /// <summary>Solde net créditeur (crédit − débit) d'un groupe de comptes.</summary>
    private static decimal SoldeNetCrediteur(IReadOnlyList<LigneBalanceDto> b, params string[] p)
        => Comptes(b, p).Sum(x => x.SoldeFinalCredit - x.SoldeFinalDebit);

    /// <summary>Valeurs brutes (comptes d'actif à solde débiteur).</summary>
    private static decimal SoldeDebiteur(IReadOnlyList<LigneBalanceDto> b, params string[] p)
        => Math.Max(0, SoldeNetDebiteur(b, p));

    /// <summary>Amortissements / dépréciations (comptes à solde créditeur).</summary>
    private static decimal SoldeCrediteur(IReadOnlyList<LigneBalanceDto> b, params string[] p)
        => Math.Max(0, SoldeNetCrediteur(b, p));

    /// <summary>Tiers / trésorerie : seuls les comptes individuellement débiteurs vont à l'actif (hors comptes de dépréciation 49/59).</summary>
    private static decimal DebiteursTiers(IReadOnlyList<LigneBalanceDto> b, params string[] p)
        => Comptes(b, p).Where(x => !EstHorsTiers(x.Numero))
                        .Sum(x => Math.Max(0, x.SoldeFinalDebit - x.SoldeFinalCredit));

    /// <summary>Tiers / trésorerie : seuls les comptes individuellement créditeurs vont au passif.</summary>
    private static decimal CrediteursTiers(IReadOnlyList<LigneBalanceDto> b, params string[] p)
        => Comptes(b, p).Where(x => !EstHorsTiers(x.Numero))
                        .Sum(x => Math.Max(0, x.SoldeFinalCredit - x.SoldeFinalDebit));

    /// <summary>Comptes présentés dans d'autres rubriques : dépréciations (49, 59), écarts de conversion (478/479), créances et dettes HAO (48x).</summary>
    private static bool EstHorsTiers(string numero) =>
        numero.StartsWith("49") || numero.StartsWith("59") ||
        numero.StartsWith("478") || numero.StartsWith("479") ||
        (numero.StartsWith("48") && numero.Length >= 3);
}
