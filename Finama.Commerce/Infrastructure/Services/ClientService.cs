using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Finama.Core.Validation;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface IClientService
{
    Task<List<ClientDto>> RechercherAsync(string? recherche, CancellationToken ct = default);
    Task<ClientDetailDto?> ObtenirAsync(Guid id, CancellationToken ct = default);
    Task<ClientDto> CreerAsync(SaveClientRequest request, CancellationToken ct = default);
    Task<ClientDto> ModifierAsync(Guid id, SaveClientRequest request, CancellationToken ct = default);
    Task<ClientDto> DefinirActifAsync(Guid id, bool actif, CancellationToken ct = default);
}

public class ClientService : IClientService
{
    private readonly ICommerceDbContext _db;
    private readonly ICurrentUser _currentUser;

    public ClientService(ICommerceDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<ClientDto>> RechercherAsync(string? recherche, CancellationToken ct = default)
    {
        var q = _db.Clients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(recherche))
        {
            var r = recherche.Trim().ToLower();
            var rTel = r.Replace(" ", "");   // « 90 00 » trouve « +22890000001 » comme « 90 00 00 01 »
            q = q.Where(c => c.Nom.ToLower().Contains(r)
                             || c.Code.ToLower().Contains(r)
                             || (rTel.Length >= 3 && c.Telephone != null && c.Telephone.Replace(" ", "").Contains(rTel)));
        }

        var liste = await q.OrderBy(c => c.Nom).Take(50).ToListAsync(ct);
        return liste.Select(Vers).ToList();
    }

    public async Task<ClientDetailDto?> ObtenirAsync(Guid id, CancellationToken ct = default)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client == null) return null;

        var ventes = _db.Ventes.AsNoTracking().Where(v => v.ClientId == id && v.Statut == StatutVente.Validee);

        var totalAchats = await ventes.SumAsync(v => (decimal?)v.TotalTTC, ct) ?? 0;

        var achats = await ventes
            .OrderByDescending(v => v.DateVente)
            .Take(20)
            .Select(v => new AchatClientDto(v.Id, v.NumeroTicket, v.DateVente, v.TotalTTC))
            .ToListAsync(ct);

        var fidelite = await _db.MouvementsFidelite.AsNoTracking()
            .Where(m => m.ClientId == id)
            .OrderByDescending(m => m.DateMouvement)
            .Take(20)
            .Select(m => new MouvementFideliteDto(m.Type, m.Points, m.SoldeApres, m.DateMouvement, m.Motif))
            .ToListAsync(ct);

        return new ClientDetailDto(Vers(client), totalAchats, achats, fidelite);
    }

    public async Task<ClientDto> CreerAsync(SaveClientRequest request, CancellationToken ct = default)
    {
        var v = Valider(request);
        var tel = v.Telephone;

        if (tel != null && await _db.Clients.AnyAsync(c => c.Telephone == tel, ct))
            throw new BusinessRuleException("Un client avec ce numéro de téléphone existe déjà.");

        var numero = await _db.Clients.CountAsync(ct) + 1;
        string code;
        do
        {
            code = $"CLI-{numero++:D5}";
        } while (await _db.Clients.AnyAsync(c => c.Code == code, ct));

        var client = new Client
        {
            TenantId = _currentUser.TenantId,
            Code = code,
            Nom = v.Nom,
            Type = request.Type,
            Telephone = tel,
            Email = v.Email,
            Adresse = v.Adresse,
            Nif = v.Nif,
            PlafondCredit = v.Plafond,
            DateCreation = DateTime.UtcNow,
            EstActif = true
        };

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);
        return Vers(client);
    }

    public async Task<ClientDto> ModifierAsync(Guid id, SaveClientRequest request, CancellationToken ct = default)
    {
        var v = Valider(request);
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Client introuvable.");

        var tel = v.Telephone;
        if (tel != null && await _db.Clients.AnyAsync(c => c.Telephone == tel && c.Id != id, ct))
            throw new BusinessRuleException("Un autre client utilise déjà ce numéro de téléphone.");

        if (v.Plafond < client.SoldeCredit && v.Plafond > 0)
            throw new BusinessRuleException($"Le plafond ne peut pas être inférieur à la créance en cours ({client.SoldeCredit:N0}).");

        client.Nom = v.Nom;
        client.Type = request.Type;
        client.Telephone = tel;
        client.Email = v.Email;
        client.Adresse = v.Adresse;
        client.Nif = v.Nif;
        client.PlafondCredit = v.Plafond;

        await _db.SaveChangesAsync(ct);
        return Vers(client);
    }

    public async Task<ClientDto> DefinirActifAsync(Guid id, bool actif, CancellationToken ct = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new NotFoundException("Client introuvable.");

        client.EstActif = actif;
        await _db.SaveChangesAsync(ct);
        return Vers(client);
    }

    private sealed record ClientValide(string Nom, string? Telephone, string? Email, string? Adresse, string? Nif, decimal Plafond);

    /// <summary>Contrôle et normalise toutes les zones (téléphone E.164, e-mail, longueurs, plafond).</summary>
    private static ClientValide Valider(SaveClientRequest r)
    {
        try
        {
            return new ClientValide(
                ValidationContact.Nom(r.Nom, "Nom du client"),
                ValidationContact.Telephone(r.Telephone),
                ValidationContact.Email(r.Email),
                ValidationContact.Texte(r.Adresse, ValidationContact.AdresseMax, "Adresse"),
                ValidationContact.Nif(r.Nif),
                ValidationContact.MontantPositif(r.PlafondCredit, "Plafond de crédit"));
        }
        catch (ArgumentException ex)
        {
            throw new BusinessRuleException(ex.Message);
        }
    }

    private static string? Nettoyer(string? valeur) => string.IsNullOrWhiteSpace(valeur) ? null : valeur.Trim();

    private static ClientDto Vers(Client c) => new(
        c.Id, c.Code, c.Nom, c.Type, c.Telephone, c.Email, c.Adresse, c.Nif,
        c.PlafondCredit, c.SoldeCredit, c.PointsFidelite, c.EstActif);
}
