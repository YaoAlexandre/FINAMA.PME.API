namespace Finama.Commerce.Core.DTOs;

/// <summary>Synthèse affichée sur l'accueil des modules : ce qui est fait, ce qui reste à faire, ce qui demande attention.</summary>
public record TableauBordCommerceDto(
    bool ActiviteConfiguree, bool UtiliseLots,
    int NbMagasins, int NbPointsDeVente, int NbArticles, int NbClients,
    bool SessionOuverte, int NbVentesJour, decimal ChiffreAffairesJour,
    int ArticlesSousSeuil, int LotsPerimes, int LotsProchesPeremption,
    decimal CreancesClients, int NbClientsEnCredit);
