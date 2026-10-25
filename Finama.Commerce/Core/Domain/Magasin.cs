using Finama.Core.Entities;

namespace Finama.Commerce.Core.Domain;

public enum TypeMagasin { PointDeVente = 1, Entrepot = 2, Transit = 3 }

public class Magasin : TenantEntity
{
    public string Code { get; set; } = "";
    public string Nom { get; set; } = "";
    public TypeMagasin Type { get; set; } = TypeMagasin.PointDeVente;
    public string? Adresse { get; set; }
    public string? Telephone { get; set; }
    public string? Responsable { get; set; }
    public bool EstPrincipal { get; set; }
    public bool EstActif { get; set; } = true;
}

public record MagasinDto(
    Guid Id,
    string Code,
    string Nom,
    TypeMagasin Type,
    string? Adresse,
    string? Telephone,
    string? Responsable,
    bool EstPrincipal,
    bool EstActif
);

public record CreerMagasinRequest(
    string Code,
    string Nom,
    TypeMagasin Type,
    string? Adresse,
    string? Telephone,
    string? Responsable,
    bool EstPrincipal
);