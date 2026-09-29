using DCenter.Server.Data;
using DCenter.Server.Entities;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class SupervisorRevocationStore(WeldReportContext db, TimeProvider time)
{
    public async Task SaveAsync(RevokedToken entry, CancellationToken ct)
    {
        var now = time.GetUtcNow();
        await db.SupervisorRevokedTokens.Where(t => t.ExpiresAt <= now).ExecuteDeleteAsync(ct);
        if (!await db.SupervisorRevokedTokens.AnyAsync(t => t.Fingerprint == entry.Fingerprint, ct))
        {
            db.SupervisorRevokedTokens.Add(new SupervisorRevokedToken { Fingerprint = entry.Fingerprint, ExpiresAt = entry.ExpiresAt });
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<RevokedToken>> LoadActiveAsync(CancellationToken ct)
    {
        var now = time.GetUtcNow();
        return await db.SupervisorRevokedTokens.AsNoTracking()
            .Where(t => t.ExpiresAt > now)
            .Select(t => new RevokedToken(t.Fingerprint, t.ExpiresAt))
            .ToListAsync(ct);
    }
}
