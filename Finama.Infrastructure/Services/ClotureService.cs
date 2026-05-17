using Finama.Core.Entities;
using Finama.Infrastructure.Data; // À adapter selon le namespace de ton DbContext
using Microsoft.EntityFrameworkCore;

namespace Finama.Infrastructure.Services;

public interface IClotureService
{
    Task CloturerExerciceAsync(Guid tenantId, Guid exerciceIdActuel, Guid utilisateurId);
}

public class ClotureService : IClotureService
{
    private readonly AppDbContext _db;

    public ClotureService(AppDbContext db)
    {
        _db = db;
    }

    public async Task CloturerExerciceAsync(Guid tenantId, Guid exerciceIdActuel, Guid utilisateurId)
    {
        // Utilisation d'une transaction isolée pour garantir l'intégrité des écritures générées
        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 1. Récupération et validation de l'exercice à clôturer
            var exerciceActuel = await _db.Exercices
                .FirstOrDefaultAsync(e => e.Id == exerciceIdActuel && e.TenantId == tenantId);

            if (exerciceActuel == null)
                throw new KeyNotFoundException("Exercice comptable introuvable.");

            if (exerciceActuel.EstCloture)
                throw new InvalidOperationException("Cet exercice est déjà clôturé.");

            // 2. Sécurité réglementaire : Bloquer s'il reste des écritures en Brouillon
            var aDesBrouillons = await _db.Ecritures
                .AnyAsync(e => e.ExerciceId == exerciceIdActuel && e.Statut == StatutEcriture.Brouillon && e.TenantId == tenantId);

            if (aDesBrouillons)
                throw new InvalidOperationException("Impossible de clôturer : Certaines écritures de cet exercice sont encore au statut Brouillon.");

            // 3. Calcul des balances de chaque compte mouvementé (uniquement les écritures Validées)
            var lignesMouvements = await _db.LignesEcriture
                .Where(l => l.Ecriture.ExerciceId == exerciceIdActuel && l.Ecriture.Statut == StatutEcriture.Validee && l.TenantId == tenantId)
                .GroupBy(l => new { l.CompteId, l.Compte.Numero, l.Compte.Libelle, l.Compte.Type })
                .Select(g => new
                {
                    g.Key.CompteId,
                    g.Key.Numero,
                    g.Key.Libelle,
                    g.Key.Type,
                    TotalDebit = g.Sum(l => l.Debit),
                    TotalCredit = g.Sum(l => l.Credit)
                })
                .ToListAsync();

            // Validation de la partie double globale avant traitement
            if (lignesMouvements.Sum(x => x.TotalDebit) != lignesMouvements.Sum(x => x.TotalCredit))
                throw new InvalidOperationException("Erreur critique : La balance globale de l'exercice est déséquilibrée.");

            // 4. Détermination du Résultat de l'Exercice (Solde des classes 6 & 7)
            var charges = lignesMouvements.Where(l => l.Type == TypeCompte.ResultatCharge).ToList();
            var produits = lignesMouvements.Where(l => l.Type == TypeCompte.ResultatProduit).ToList();

            decimal totalCharges = charges.Sum(c => c.TotalDebit - c.TotalCredit);
            decimal totalProduits = produits.Sum(p => p.TotalCredit - p.TotalDebit);
            decimal resultatNet = totalProduits - totalCharges;

            // Identification du compte SYSCOHADA adéquat selon le résultat
            var compteResultatNumero = resultatNet >= 0 ? "131000" : "139000"; // 131000 = Bénéfice, 139000 = Perte
            var compteResultat = await _db.CompteComptables
                .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Numero == compteResultatNumero);

            if (compteResultat == null)
                throw new InvalidOperationException($"Configuration manquante : Le compte réglementaire du résultat net ({compteResultatNumero}) n'existe pas dans votre plan comptable.");

            // Création de l'en-tête de l'écriture d'OD de clôture des comptes de gestion
            var ecritureSoldeGestion = new EcritureComptable
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ExerciceId = exerciceIdActuel,
                Reference = $"CLOT-GES-{exerciceActuel.Annee}",
                DateEcriture = exerciceActuel.DateFin,
                Libelle = $"Solde des comptes de gestion - Clôture {exerciceActuel.Annee}",
                Journal = "OD",
                Statut = StatutEcriture.Validee,
                UtilisateurId = utilisateurId
            };

            // Vider les charges (Débit > Crédit, donc on ajoute une ligne au Crédit pour solder à 0)
            foreach (var c in charges)
            {
                decimal soldeCharge = c.TotalDebit - c.TotalCredit;
                if (soldeCharge != 0)
                {
                    ecritureSoldeGestion.Lignes.Add(new LigneEcriture
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CompteId = c.CompteId,
                        Debit = 0,
                        Credit = soldeCharge,
                        Devise = "XOF" // À remplacer par ta propriété ou constante de devise par défaut
                    });
                }
            }

            // Vider les produits (Crédit > Débit, donc on ajoute une ligne au Débit pour solder à 0)
            foreach (var p in produits)
            {
                decimal soldeProduit = p.TotalCredit - p.TotalDebit;
                if (soldeProduit != 0)
                {
                    ecritureSoldeGestion.Lignes.Add(new LigneEcriture
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CompteId = p.CompteId,
                        Debit = soldeProduit,
                        Credit = 0,
                        Devise = "XOF"
                    });
                }
            }

            // Équilibrer l'OD avec le compte de résultat (131 ou 139)
            if (resultatNet >= 0)
            {
                ecritureSoldeGestion.Lignes.Add(new LigneEcriture { Id = Guid.NewGuid(), TenantId = tenantId, CompteId = compteResultat.Id, Debit = 0, Credit = resultatNet, Devise = "XOF" });
            }
            else
            {
                ecritureSoldeGestion.Lignes.Add(new LigneEcriture { Id = Guid.NewGuid(), TenantId = tenantId, CompteId = compteResultat.Id, Debit = Math.Abs(resultatNet), Credit = 0, Devise = "XOF" });
            }

            _db.Ecritures.Add(ecritureSoldeGestion);

            // 5. Initialisation automatique de l'exercice comptable suivant (N+1)
            int prochaineAnnee = exerciceActuel.Annee + 1;
            var nouvelExercice = new ExerciceComptable
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Annee = prochaineAnnee,
                DateDebut = new DateTime(prochaineAnnee, 1, 1),
                DateFin = new DateTime(prochaineAnnee, 12, 31),
                EstCloture = false
            };
            _db.Exercices.Add(nouvelExercice);

            // 6. Génération de l'écriture de Report à Nouveau (RAN) dans l'exercice N+1
            var ecritureRAN = new EcritureComptable
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                ExerciceId = nouvelExercice.Id,
                Reference = $"RAN-{prochaineAnnee}",
                DateEcriture = nouvelExercice.DateDebut,
                Libelle = $"Report à Nouveau - Ouverture Exercice {prochaineAnnee}",
                Journal = "RAN",
                Statut = StatutEcriture.Validee,
                UtilisateurId = utilisateurId
            };

            // Filtrage pour ne reporter que les comptes de Bilan et Trésorerie (Classes 1 à 5)
            var comptesBilan = lignesMouvements
                .Where(l => l.Type == TypeCompte.Bilan || l.Type == TypeCompte.Tresorerie || l.CompteId == compteResultat.Id)
                .ToList();

            foreach (var b in comptesBilan)
            {
                decimal debitsAccumules = b.TotalDebit;
                decimal creditsAccumules = b.TotalCredit;

                // On intègre l'impact immédiat de la clôture sur le compte de résultat choisi
                if (b.CompteId == compteResultat.Id)
                {
                    if (resultatNet >= 0) creditsAccumules += resultatNet;
                    else debitsAccumules += Math.Abs(resultatNet);
                }

                decimal soldeFinalDebit = debitsAccumules > creditsAccumules ? debitsAccumules - creditsAccumules : 0;
                decimal soldeFinalCredit = creditsAccumules > debitsAccumules ? creditsAccumules - debitsAccumules : 0;

                if (soldeFinalDebit > 0 || soldeFinalCredit > 0)
                {
                    ecritureRAN.Lignes.Add(new LigneEcriture
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        CompteId = b.CompteId,
                        Debit = soldeFinalDebit,
                        Credit = soldeFinalCredit,
                        Devise = "XOF"
                    });
                }
            }

            _db.Ecritures.Add(ecritureRAN);

            // 7. Passage à l'état clos et datage de l'exercice actuel
            exerciceActuel.EstCloture = true;
            exerciceActuel.ClotureLe = DateTime.UtcNow;

            // Sauvegarde de l'ensemble des entités générées
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}