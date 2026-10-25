using Finama.Commerce.Application.Common;
using Finama.Commerce.Core.Domain;
using Finama.Commerce.Core.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Finama.Commerce.Infrastructure.Services;

public interface IMagasinService
{
    Task<List<MagasinDto>> GetMagasinsAsync(CancellationToken ct = default);
    Task<MagasinDto> CreerMagasinAsync(CreerMagasinRequest request, CancellationToken ct = default);
    Task<bool> DefinirCommePrincipalAsync(Guid magasinId, CancellationToken ct = default);
}

public class MagasinService : IMagasinService
{
    private readonly ICommerceDbContext _db;
    private readonly ICurrentUser _currentUser;

    public MagasinService(ICommerceDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<List<MagasinDto>> GetMagasinsAsync(CancellationToken ct = default)
    {
        return await _db.Magasins
            .OrderByDescending(m => m.EstPrincipal)
            .ThenBy(m => m.Nom)
            .Select(m => new MagasinDto(
                m.Id, m.Code, m.Nom, m.Type, m.Adresse, m.Telephone, m.Responsable, m.EstPrincipal, m.EstActif
            ))
            .ToListAsync(ct);
    }

    public async Task<MagasinDto> CreerMagasinAsync(CreerMagasinRequest request, CancellationToken ct = default)
    {
        return await _db.ExecuterAsync(async () =>
        {
            var existeDeja = await _db.Magasins.AnyAsync(m => m.Code == request.Code, ct);
            if (existeDeja)
                throw new BusinessRuleException($"Un magasin avec le code '{request.Code}' existe déjà.");

            // Si aucun magasin n'existe, ou si explicitement demandé principal
            var totalMagasins = await _db.Magasins.CountAsync(ct);
            bool definirPrincipal = request.EstPrincipal || totalMagasins == 0;

            if (definirPrincipal)
            {
                var principaux = await _db.Magasins.Where(m => m.EstPrincipal).ToListAsync(ct);
                foreach (var p in principaux) p.EstPrincipal = false;
            }

            var magasin = new Magasin
            {
                TenantId = _currentUser.TenantId,
                Code = request.Code.ToUpperInvariant(),
                Nom = request.Nom,
                Type = request.Type,
                Adresse = request.Adresse,
                Telephone = request.Telephone,
                Responsable = request.Responsable,
                EstPrincipal = definirPrincipal,
                EstActif = true
            };

            _db.Magasins.Add(magasin);
            await _db.SaveChangesAsync(ct);

            return new MagasinDto(magasin.Id, magasin.Code, magasin.Nom, magasin.Type, magasin.Adresse, magasin.Telephone, magasin.Responsable, magasin.EstPrincipal, magasin.EstActif);
        }, ct);
    }

    public async Task<bool> DefinirCommePrincipalAsync(Guid magasinId, CancellationToken ct = default)
    {
        return await _db.ExecuterAsync(async () =>
        {
            var cible = await _db.Magasins.FirstOrDefaultAsync(m => m.Id == magasinId, ct)
                ?? throw new NotFoundException("Magasin introuvable.");

            var actuelsPrincipaux = await _db.Magasins.Where(m => m.EstPrincipal).ToListAsync(ct);
            foreach (var p in actuelsPrincipaux) p.EstPrincipal = false;

            cible.EstPrincipal = true;
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }
}