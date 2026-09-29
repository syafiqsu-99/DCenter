using DCenter.Server.Data;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Services;

// One reference table (WPS, MRN, BPVC IX): its CSV columns, key and how values map onto the entity.
public interface IReferenceTable<TEntity> where TEntity : class, new()
{
    Column[] Columns { get; }
    string FileName { get; }
    string RequiredMessage { get; }
    string EntityName { get; }
    int IdOf(TEntity entity);
    string?[] Values(TEntity entity);
    void Write(TEntity entity, string?[] values);
    IQueryable<TEntity> Ordered(IQueryable<TEntity> query);
    IQueryable<TEntity> SameRequiredKey(IQueryable<TEntity> query, string?[] values);
}

public class ReferenceTableService<TEntity>(
    WeldReportContext db, IReferenceTable<TEntity> table, ILogger<ReferenceTableService<TEntity>> logger)
    where TEntity : class, new()
{
    public const string SaveConflict =
        "This change could not be saved because the table changed at the same time. Reload the list and try again.";

    private DbSet<TEntity> Set => db.Set<TEntity>();

    public Task<List<TEntity>> ListAsync(CancellationToken ct)
        => table.Ordered(Set.AsNoTracking()).ToListAsync(ct);

    public async Task<ServiceResult<TEntity>> CreateAsync(string?[] values, CancellationToken ct)
    {
        if (await ValidateAsync(values, 0, ct) is { } error) return error.As<TEntity>();
        var entity = new TEntity();
        table.Write(entity, values);
        Set.Add(entity);
        return await SaveAsync(entity, ct);
    }

    public async Task<ServiceResult<bool>> UpdateAsync(int id, string?[] values, CancellationToken ct)
    {
        var entity = await Set.FindAsync([id], ct);
        if (entity is null) return ServiceResult<bool>.NotFound();
        if (await ValidateAsync(values, id, ct) is { } error) return error;
        table.Write(entity, values);
        return await SaveAsync(true, ct);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await Set.FindAsync([id], ct);
        if (entity is null) return ServiceResult<bool>.NotFound();
        Set.Remove(entity);
        return await SaveAsync(true, ct);
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct)
        => Export(table.Columns, (await ListAsync(ct)).Select(table.Values));

    public async Task<ServiceResult<ImportCounts>> ImportAsync(IFormFile? file, CancellationToken ct)
    {
        var sheet = await ReadAsync(file, table.Columns, ct);
        if (sheet.Errors.Count > 0) return ServiceResult<ImportCounts>.Fail(string.Join(" ", sheet.Errors));

        var existing = await Set.ToListAsync(ct);
        var counts = Upsert(table.Columns, sheet.Rows, existing, table.Values, table.Write, () =>
        {
            var entity = new TEntity();
            Set.Add(entity);
            return entity;
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "{Table} import failed to save", table.EntityName);
            return ServiceResult<ImportCounts>.Fail(ImportSaveConflict, StatusCodes.Status409Conflict);
        }
        return ServiceResult<ImportCounts>.Ok(counts);
    }

    private async Task<ServiceResult<bool>?> ValidateAsync(string?[] values, int excludeId, CancellationToken ct)
    {
        var columns = table.Columns;
        if (columns.Select((c, i) => (c, i)).Any(x => x.c.Required && Clean(values[x.i]) is null))
            return ServiceResult<bool>.Fail(table.RequiredMessage);
        if (LengthError(columns, values) is { } tooLong) return ServiceResult<bool>.Fail(tooLong);

        var key = KeyOf(columns, values);
        var candidates = await table.SameRequiredKey(Set.AsNoTracking(), values).ToListAsync(ct);
        return candidates.Any(e => table.IdOf(e) != excludeId && KeyOf(columns, table.Values(e)) == key)
            ? ServiceResult<bool>.Fail($"A row with this {KeyLabel(columns)} already exists. Edit that row instead.", StatusCodes.Status409Conflict)
            : null;
    }

    private async Task<ServiceResult<T>> SaveAsync<T>(T value, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return ServiceResult<T>.Ok(value);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "{Table} save failed", table.EntityName);
            return ServiceResult<T>.Fail(SaveConflict, StatusCodes.Status409Conflict);
        }
    }
}
