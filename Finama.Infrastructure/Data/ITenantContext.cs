using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finama.Infrastructure.Data
{
    // ─── Interface pour injecter le tenant courant ────────────────────────────────
    public interface ITenantContext
    {
        Guid? TenantId { get; }
    }
}
