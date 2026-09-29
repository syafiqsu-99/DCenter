namespace DCenter.Server.Services;

public class SupervisorRevocationStore(StoredProcedures sp, TimeProvider time)
{
    public Task SaveAsync(RevokedToken entry, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Supervisor_RevokeToken", ct,
            Sql.VarChar("@Fingerprint", entry.Fingerprint, 64), Sql.DateTimeOffset("@ExpiresAt", entry.ExpiresAt),
            Sql.DateTimeOffset("@Now", time.GetUtcNow())));

    public Task<List<RevokedToken>> LoadActiveAsync(CancellationToken ct)
        => sp.QueryAsync<RevokedToken>("SP_DCenter_Supervisor_ActiveRevocations", ct, Sql.DateTimeOffset("@Now", time.GetUtcNow()));
}
