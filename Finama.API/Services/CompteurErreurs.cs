using System.Collections.Concurrent;

namespace Finama.API.Services;

/// <summary>Mémoire des erreurs serveur (500) de la dernière heure, pour alerter le SuperAdmin en cas de rafale.</summary>
public static class CompteurErreurs
{
    private static readonly ConcurrentQueue<(DateTime Quand, string Chemin)> Erreurs = new();

    public static void Signaler(string? chemin)
    {
        Erreurs.Enqueue((DateTime.UtcNow, chemin ?? ""));
        while (Erreurs.TryPeek(out var plusAncienne) && plusAncienne.Quand < DateTime.UtcNow.AddHours(-1))
            Erreurs.TryDequeue(out _);
    }

    /// <summary>Erreurs depuis cette date, et la route la plus touchée.</summary>
    public static (int Nombre, string? CheminPrincipal) Depuis(DateTime depuis)
    {
        var liste = Erreurs.Where(e => e.Quand >= depuis).ToList();
        var principal = liste.GroupBy(e => e.Chemin).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();
        return (liste.Count, principal);
    }
}
