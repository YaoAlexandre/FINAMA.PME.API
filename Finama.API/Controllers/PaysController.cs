using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Finama.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaysController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaysController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Liste tous les pays supportés — public, sans auth.
    /// Utilisé par le frontend pour remplir le select lors de l'inscription.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Lister()
    {
        var pays = await _db.Pays
            .Where(p => p.EstActif && !p.IsDeleted)
            .OrderBy(p => p.Nom)
            .Select(p => new
            {
                p.Id,
                p.Nom,
                p.CodeISO,
                p.DeviseCode,
                p.DeviseSymbole,
                p.TauxTVAStandard,
                p.CodeFiscal,
                p.Langue,
            })
            .ToListAsync();

        return Ok(pays);
    }

    /// <summary>
    /// Détail d'un pays — utilisé pour pré-remplir le formulaire d'inscription.
    /// </summary>
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Obtenir(Guid id)
    {
        var pays = await _db.Pays
            .Where(p => p.Id == id && p.EstActif && !p.IsDeleted)
            .Select(p => new
            {
                p.Id,
                p.Nom,
                p.CodeISO,
                p.DeviseCode,
                p.DeviseSymbole,
                p.TauxTVAStandard,
                p.CodeFiscal,
                p.Langue,
            })
            .FirstOrDefaultAsync();

        if (pays is null)
            return NotFound(new { message = "Pays introuvable." });

        return Ok(pays);
    }

    /// <summary>
    /// Ajouter un nouveau pays — SuperAdmin uniquement.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> Ajouter([FromBody] AjouterPaysRequest request)
    {
        if (await _db.Pays.AnyAsync(p => p.CodeISO == request.CodeISO.ToUpper()))
            return Conflict(new { message = $"Le pays avec le code ISO '{request.CodeISO}' existe déjà." });

        var pays = new Finama.Core.Entities.PaysConfig
        {
            Nom = request.Nom,
            CodeISO = request.CodeISO.ToUpper(),
            DeviseCode = request.DeviseCode.ToUpper(),
            DeviseSymbole = request.DeviseSymbole,
            TauxTVAStandard = request.TauxTVAStandard,
            CodeFiscal = request.CodeFiscal,
            Langue = request.Langue,
            EstActif = true,
        };

        _db.Pays.Add(pays);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Obtenir), new { id = pays.Id }, pays);
    }

    /// <summary>
    /// Modifier le taux TVA d'un pays — SuperAdmin uniquement.
    /// Immédiatement effectif sans redéploiement.
    /// </summary>
    [HttpPatch("{id:guid}/tva")]
    [Authorize(Roles = "SuperAdmin")]
    public async Task<IActionResult> ModifierTVA(Guid id, [FromBody] ModifierTVARequest request)
    {
        var pays = await _db.Pays.FindAsync(id);
        if (pays is null)
            return NotFound(new { message = "Pays introuvable." });

        pays.TauxTVAStandard = request.NouveauTaux;
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Taux TVA de {pays.Nom} mis à jour : {request.NouveauTaux}%." });
    }
}

public record AjouterPaysRequest(
    string Nom,
    string CodeISO,
    string DeviseCode,
    string DeviseSymbole,
    decimal TauxTVAStandard,
    string CodeFiscal,
    string Langue = "fr"
);

public record ModifierTVARequest(decimal NouveauTaux);
