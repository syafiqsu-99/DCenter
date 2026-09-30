using DCenter.Server.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Services;

// One reference table (WPS, MRN, BPVC IX): its CSV columns, key and how values map onto the entity.
// Name picks the procedures SP_DCenter_{Name}_List/Save/Delete; Save reads the rows as JSON keyed by the CSV headers.
public interface IReferenceTable<TEntity> where TEntity : class, new()
{
    Column[] Columns { get; }
    string FileName { get; }
    string RequiredMessage { get; }
    string EntityName { get; }
    string Name { get; }
    int IdOf(TEntity entity);
    string?[] Values(TEntity entity);
    void Write(TEntity entity, string?[] values);
    SqlParameter[] RequiredKey(string?[] values);
}

public class ReferenceTableService<TEntity>(
    StoredProcedures sp, IReferenceTable<TEntity> table, ILogger<ReferenceTableService<TEntity>> logger)
    where TEntity : class, new()
{
    public const string SaveConflict =
        "This change could not be saved because the table changed at the same time. Reload the list and try again.";

    private string Procedure(string action) => $"SP_DCenter_{table.Name}_{action}";

    public Task<List<TEntity>> ListAsync(CancellationToken ct)
        => sp.EntitiesAsync<TEntity>(Procedure("List"), ct);

    public async Task<ServiceResult<TEntity>> CreateAsync(string?[] values, CancellationToken ct)
    {
        if (await ValidateAsync(values, 0, ct) is { } error) return error.As<TEntity>();
        var entity = new TEntity();
        table.Write(entity, values);
        return await TrySaveAsync(async () => (await SaveAsync([], [entity], ct)).Single());
    }

    public async Task<ServiceResult<bool>> UpdateAsync(int id, string?[] values, CancellationToken ct)
    {
        var entity = await GetAsync(id, ct);
        if (entity is null) return ServiceResult<bool>.NotFound();
        if (await ValidateAsync(values, id, ct) is { } error) return error;
        table.Write(entity, values);
        return await TrySaveAsync(async () =>
        {
            await SaveAsync([entity], [], ct);
            return true;
        });
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        if (await GetAsync(id, ct) is null) return ServiceResult<bool>.NotFound();
        return await TrySaveAsync(async () =>
        {
            await StoredProcedures.Write(sp.ExecuteAsync(Procedure("Delete"), ct, Sql.Int("@Id", id)));
            return true;
        });
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct)
        => Export(table.Columns, (await ListAsync(ct)).Select(table.Values));

    public async Task<ServiceResult<ImportCounts>> ImportAsync(IFormFile? file, CancellationToken ct)
    {
        var sheet = await ReadAsync(file, table.Columns, ct);
        if (sheet.Errors.Count > 0) return ServiceResult<ImportCounts>.Fail(string.Join(" ", sheet.Errors));

        var existing = await ListAsync(ct);
        var plan = UpsertPlan(table.Columns, sheet.Rows, existing, table.Values, table.Write, () => new TEntity());

        try
        {
            await SaveAsync(plan.Updated, plan.Added, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "{Table} import failed to save", table.EntityName);
            return ServiceResult<ImportCounts>.Fail(ImportSaveConflict, StatusCodes.Status409Conflict);
        }
        return ServiceResult<ImportCounts>.Ok(plan.Counts);
    }

    private async Task<TEntity?> GetAsync(int id, CancellationToken ct)
        => (await sp.EntitiesAsync<TEntity>(Procedure("List"), ct, Sql.Int("@Id", id))).FirstOrDefault();

    private async Task<ServiceResult<bool>?> ValidateAsync(string?[] values, int excludeId, CancellationToken ct)
    {
        var columns = table.Columns;
        if (columns.Select((c, i) => (c, i)).Any(x => x.c.Required && Clean(values[x.i]) is null))
            return ServiceResult<bool>.Fail(table.RequiredMessage);
        if (LengthError(columns, values) is { } tooLong) return ServiceResult<bool>.Fail(tooLong);

        var key = KeyOf(columns, values);
        var candidates = await sp.EntitiesAsync<TEntity>(Procedure("List"), ct, table.RequiredKey(values));
        return candidates.Any(e => table.IdOf(e) != excludeId && KeyOf(columns, table.Values(e)) == key)
            ? ServiceResult<bool>.Fail($"A row with this {KeyLabel(columns)} already exists. Edit that row instead.", StatusCodes.Status409Conflict)
            : null;
    }

    // Updates the rows that have an Id, inserts the others in order, and returns the inserted rows.
    private Task<List<TEntity>> SaveAsync(IEnumerable<TEntity> updated, IEnumerable<TEntity> added, CancellationToken ct)
    {
        var rows = updated.Select(e => (Id: (int?)table.IdOf(e), Entity: e))
            .Concat(added.Select(e => (Id: (int?)null, Entity: e)))
            .Select((r, seq) => Row(seq, r.Id, table.Values(r.Entity)));
        return StoredProcedures.Write(sp.EntitiesAsync<TEntity>(Procedure("Save"), ct, Sql.Json("@Rows", rows)));
    }

    // One JSON array per row: Seq, Id, then the values in Columns order (the procedure reads them by position).
    private static object?[] Row(int seq, int? id, string?[] values) => [seq, id, .. values];

    private async Task<ServiceResult<T>> TrySaveAsync<T>(Func<Task<T>> save)
    {
        try
        {
            return ServiceResult<T>.Ok(await save());
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "{Table} save failed", table.EntityName);
            return ServiceResult<T>.Fail(SaveConflict, StatusCodes.Status409Conflict);
        }
    }
}
