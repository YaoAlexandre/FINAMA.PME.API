using System.Security.Claims;
using Finama.Infrastructure.Data;
using Microsoft.AspNetCore.Http;

namespace Finama.Infrastructure.Services;

/// <summary>
/// Résout le TenantId courant depuis le JWT (claim "tenant_id").
/// Injecté en Scoped — une instance par requête HTTP.
/// </summary>
public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; }

    public TenantContext(IHttpContextAccessor accessor)
    {
        var claim = accessor.HttpContext?.User?.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(claim, out var id))
            TenantId = id;
    }
}
