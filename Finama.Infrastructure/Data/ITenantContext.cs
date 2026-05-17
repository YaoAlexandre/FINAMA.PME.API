namespace Finama.Infrastructure.Data
{
    // ─── Interface pour injecter le tenant courant ────────────────────────────────
    public interface ITenantContext
    {
        Guid? TenantId { get; }
    }
}
