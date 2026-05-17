using System;
using System.Threading.Tasks;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services;

public interface IDeviseService
{
    Task<decimal> ObtenirTauxEchangeAsync(string codeDeviseOrigine, string codeDeviseBaseTenant);
}

public class DeviseService : IDeviseService
{
    private readonly AppDbContext _db;

    public DeviseService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<decimal> ObtenirTauxEchangeAsync(string codeDeviseOrigine, string codeDeviseBaseTenant)
    {
        if (codeDeviseOrigine == codeDeviseBaseTenant) return 1.0m;

        // Récupérer les taux par rapport au Dollar
        var tauxOrigine = await _db.Devises.FirstOrDefaultAsync(d => d.Code == codeDeviseOrigine && d.EstActive);
        var tauxPivot = await _db.Devises.FirstOrDefaultAsync(d => d.Code == codeDeviseBaseTenant && d.EstActive);

        if (tauxOrigine == null || tauxPivot == null)
        {
            throw new Exception("Une des devises demandées n'est pas supportée par la plateforme Finama.");
        }

        // Formule du taux croisé : Taux Pivot / Taux Origine
        decimal tauxCroise = tauxPivot.TauxBaseDollar / tauxOrigine.TauxBaseDollar;

        return Math.Round(tauxCroise, 4);
    }
}