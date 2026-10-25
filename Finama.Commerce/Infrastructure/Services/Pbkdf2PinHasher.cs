using System.Security.Cryptography;
using Finama.Commerce.Application.Common;

namespace Finama.Commerce.Infrastructure.Services;

/// <summary>
/// Hash de PIN : PBKDF2-SHA256, sel aléatoire par PIN. Format stocké : "v1.{itérations}.{selBase64}.{hashBase64}".
/// Un PIN court a peu d'entropie : la protection réelle vient du verrouillage après échecs (PosService), pas du hash seul.
/// </summary>
public class Pbkdf2PinHasher : IPinHasher
{
    private const int Iterations = 100_000;
    private const int TailleSel = 16;
    private const int TailleHash = 32;

    public string Hash(string pin)
    {
        var sel = RandomNumberGenerator.GetBytes(TailleSel);
        var hash = Rfc2898DeriveBytes.Pbkdf2(pin, sel, Iterations, HashAlgorithmName.SHA256, TailleHash);
        return $"v1.{Iterations}.{Convert.ToBase64String(sel)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string pin, string hash)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrEmpty(hash)) return false;

        var parties = hash.Split('.');
        if (parties.Length != 4 || parties[0] != "v1" || !int.TryParse(parties[1], out var iterations)) return false;

        try
        {
            var sel = Convert.FromBase64String(parties[2]);
            var attendu = Convert.FromBase64String(parties[3]);
            var calcule = Rfc2898DeriveBytes.Pbkdf2(pin, sel, iterations, HashAlgorithmName.SHA256, attendu.Length);
            return CryptographicOperations.FixedTimeEquals(calcule, attendu);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
