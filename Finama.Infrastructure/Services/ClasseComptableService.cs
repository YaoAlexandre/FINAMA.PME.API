using Finama.Core.Entities;
using Finama.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services;

public interface IClasseComptableService
{
    Task<List<ClasseComptable>> ObtenirToutesAsync();
    Task AjouterAsync(ClasseComptable nouvelleClasse);
    Task ModifierLibelleAsync(int numero, string nouveauLibelle);
}

public class ClasseComptableService : IClasseComptableService
{
    private readonly AppDbContext _db;

    public ClasseComptableService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ClasseComptable>> ObtenirToutesAsync()
    {
        // Le filtre de TenantId s'applique automatiquement ici grâce à ton AppDbContext
        return await _db.ClassesComptables
            .OrderBy(c => c.Numero)
            .ToListAsync();
    }

    public async Task AjouterAsync(ClasseComptable nouvelleClasse)
    {
        var existe = await _db.ClassesComptables.AnyAsync(c => c.Numero == nouvelleClasse.Numero);
        if (existe)
            throw new InvalidOperationException($"La classe {nouvelleClasse.Numero} existe déjà.");

        await _db.ClassesComptables.AddAsync(nouvelleClasse);
        await _db.SaveChangesAsync();
    }

    public async Task ModifierLibelleAsync(int numero, string nouveauLibelle)
    {
        var classe = await _db.ClassesComptables.FirstOrDefaultAsync(c => c.Numero == numero);
        if (classe is null)
            throw new KeyNotFoundException("Classe introuvable ou non autorisée pour ce compte entreprise.");

        classe.Libelle = nouveauLibelle;
        await _db.SaveChangesAsync();
    }
}