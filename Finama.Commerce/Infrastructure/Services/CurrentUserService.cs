using System.Security.Claims;
using Finama.Commerce.Application.Common;
using Microsoft.AspNetCore.Http;

namespace Finama.Commerce.Infrastructure.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private readonly HttpContext? _httpContext = httpContextAccessor.HttpContext;

    public Guid UserId
    {
        get
        {
            var claim = _httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier) ?? _httpContext?.User?.FindFirst("sub");
            return claim != null && Guid.TryParse(claim.Value, out var id) ? id : Guid.Empty;
        }
    }

    public Guid TenantId
    {
        get
        {
            var claim = _httpContext?.User?.FindFirst("tenant_id") ?? _httpContext?.User?.FindFirst("TenantId");
            if (claim != null && Guid.TryParse(claim.Value, out var id)) return id;
            throw new UnauthorizedAccessException("Aucun tenant actif trouvé dans le jeton.");
        }
    }

    public bool EstAutorise(string permission)
    {
        // Vérifie si l'utilisateur possède la permission requise dans ses claims
        return _httpContext?.User?.HasClaim("permission", permission) ?? false;
    }
}