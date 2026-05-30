using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Core.DTOs
{
    public record EntrepriseUpdateRequest(
     string Nom,
     string? Adresse,
     string? Telephone,
     string? NumeroFiscal,
     string? BanqueNom,
     string? BanqueBIC,
     decimal TauxTVA
 );
}
