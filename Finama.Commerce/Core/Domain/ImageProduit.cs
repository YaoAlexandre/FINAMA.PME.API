using Finama.Core.Entities;

namespace Finama.Commerce.Core.Domain;

/// <summary>
/// Photo facultative d'un article (une par article), redimensionnée par le navigateur avant l'envoi (≈ 600 px, quelques dizaines de Ko).
/// Stockée à part du produit pour que les listes d'articles restent légères.
/// JetonPublic : identifiant imprévisible de l'adresse publique de l'image ; il change à chaque nouvelle photo,
/// ce qui permet une mise en cache permanente par le navigateur (et l'affichage hors connexion).
/// </summary>
public class ImageProduit : TenantEntity
{
    public Guid ProduitId { get; set; }
    public byte[] Contenu { get; set; } = [];
    public string TypeMime { get; set; } = "image/jpeg";
    public int Taille { get; set; }
    public Guid JetonPublic { get; set; } = Guid.NewGuid();
}
