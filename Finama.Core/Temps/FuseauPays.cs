namespace Finama.Core.Temps;

/// <summary>
/// Heure locale d'une entreprise d'après le code ISO de son pays.
/// Les dates sont stockées en UTC ; le serveur (Docker) tourne en UTC : <c>ToLocalTime()</c> n'a donc
/// aucun sens pour un document client (reçu, export). Utiliser <see cref="VersLocal"/> à la place.
/// </summary>
public static class FuseauPays
{
    // Identifiants IANA : reconnus par .NET sous Linux comme sous Windows (conversion ICU).
    private static readonly Dictionary<string, string> Fuseaux = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TG"] = "Africa/Lome",
        ["ST"] = "Africa/Sao_Tome",
        ["SN"] = "Africa/Dakar",
        ["CI"] = "Africa/Abidjan",
        ["BJ"] = "Africa/Porto-Novo",
        ["BF"] = "Africa/Ouagadougou",
        ["ML"] = "Africa/Bamako",
        ["NE"] = "Africa/Niamey",
        ["CM"] = "Africa/Douala",
        ["GA"] = "Africa/Libreville",
        ["CG"] = "Africa/Brazzaville",
        ["GN"] = "Africa/Conakry",
        ["TD"] = "Africa/Ndjamena",
        ["CF"] = "Africa/Bangui",
        ["GQ"] = "Africa/Malabo",
        ["GW"] = "Africa/Bissau",
        ["KM"] = "Indian/Comoro",
        ["CD"] = "Africa/Kinshasa",
    };

    public static TimeZoneInfo Pour(string? codeIso)
    {
        if (codeIso is not null && Fuseaux.TryGetValue(codeIso.Trim(), out var id))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>Convertit une date UTC (ou de type non précisé, considérée UTC) vers l'heure locale du pays.</summary>
    public static DateTime VersLocal(DateTime utc, string? codeIso)
    {
        var u = utc.Kind == DateTimeKind.Utc ? utc : DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(u, Pour(codeIso));
    }
}
