using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Core.DTOs;

public record CreerDemandeAccesRequest(
 string NomEntreprise,
 string NomContact,
 string PrenomContact,
 string Email,
 string Telephone,
 Guid PaysId,
 string? Message
);

public record TraiterDemandeAccesRequest(
    bool Approuver,
    string? MotifRefus
);

public record DemandeAccesResponse(
    Guid Id,
    string NomEntreprise,
    string NomContact,
    string PrenomContact,
    string Email,
    string Telephone,
    string NomPays,
    string? Message,
    string Statut,
    DateTime DateDemande
);
