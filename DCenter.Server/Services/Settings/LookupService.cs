using System.Data;
using System.Globalization;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Services;

public class LookupService(StoredProcedures sp, ILogger<LookupService> logger)
{
    public static readonly string[] Categories =
        ["Process", ConsumableItemService.LookupSize, ConsumableItemService.LookupType, ConsumableItemService.LookupBrand];

    public const int MaxValueLength = 200;

    private static readonly Column[] Columns =
    [
        new("Category", Required: true, Key: true) { MaxLength = 50 },
        new("Value", Required: true, Key: true) { MaxLength = MaxValueLength },
        new("SortOrder", Required: false, Key: false),
        new("IsActive", Required: false, Key: false, "IsActive", "Active"),
    ];

    public async Task<List<LookupDto>> ListAsync(string? category, CancellationToken ct)
        => (await ItemsAsync(string.IsNullOrWhiteSpace(category) ? null : category, ct)).Select(ToDto).ToList();

    public async Task<ServiceResult<LookupDto>> CreateAsync(LookupUpsert dto, CancellationToken ct)
    {
        var (category, value, error) = Validate(dto);
        if (error is not null) return ServiceResult<LookupDto>.Fail(error);
        if (await IsDuplicateAsync(category, value, null, ct)) return Duplicate<LookupDto>(category, value);

        var l = new LookupItem { Category = category, Value = value, SortOrder = dto.SortOrder, IsActive = dto.IsActive };
        var created = await TrySaveAsync([], [l], ct);
        if (created is null) return Duplicate<LookupDto>(category, value);
        return ServiceResult<LookupDto>.Ok(ToDto(created.Single()));
    }

    public async Task<ServiceResult<bool>> UpdateAsync(int id, LookupUpsert dto, CancellationToken ct)
    {
        var (category, value, error) = Validate(dto);
        if (error is not null) return ServiceResult<bool>.Fail(error);

        if (!await sp.ScalarAsync<bool>("SP_DCenter_Lookup_Exists", ct, Sql.Int("@Id", id))) return ServiceResult<bool>.NotFound();
        if (await IsDuplicateAsync(category, value, id, ct)) return Duplicate<bool>(category, value);

        var l = new LookupItem { Id = id, Category = category, Value = value, SortOrder = dto.SortOrder, IsActive = dto.IsActive };
        if (await TrySaveAsync([l], [], ct) is null) return Duplicate<bool>(category, value);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task ReorderAsync(List<int> ids, CancellationToken ct)
    {
        var rows = new DataTable();
        rows.Columns.Add("Id", typeof(int));
        rows.Columns.Add("SortOrder", typeof(int));
        foreach (var id in ids.Distinct()) rows.Rows.Add(id, ids.IndexOf(id));
        await StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Lookup_Reorder", ct, Sql.Table("@Rows", "dbo.TT_DCenter_IdOrder", rows)));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        if (!await sp.ScalarAsync<bool>("SP_DCenter_Lookup_Exists", ct, Sql.Int("@Id", id))) return ServiceResult<bool>.NotFound();
        await StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Lookup_Delete", ct, Sql.Int("@Id", id)));
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct)
        => Export(Columns, (await ItemsAsync(null, ct)).Select(Values));

    public async Task<ServiceResult<ImportCounts>> ImportAsync(IFormFile? file, CancellationToken ct)
    {
        var sheet = await ReadAsync(file, Columns, ct);
        if (sheet.Errors.Count > 0) return ServiceResult<ImportCounts>.Fail(string.Join(" ", sheet.Errors));

        var existing = await ItemsAsync(null, ct);
        var plan = UpsertPlan(Columns, sheet.Rows.Select(NormalizeRow), existing, Values, Write, () => new LookupItem());

        try
        {
            await SaveAsync(plan.Updated, plan.Added, ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Dropdown list import failed to save");
            return ServiceResult<ImportCounts>.Fail(ImportSaveConflict, StatusCodes.Status409Conflict);
        }
        return ServiceResult<ImportCounts>.Ok(plan.Counts);
    }

    internal static (string Category, string Value, string? Error) Validate(LookupUpsert dto)
    {
        var category = (dto.Category ?? string.Empty).Trim();
        var value = (dto.Value ?? string.Empty).Trim();
        if (!Categories.Contains(category))
            return (category, value, $"Category must be one of: {string.Join(", ", Categories)}");
        if (value.Length == 0) return (category, value, "Value is required.");
        if (value.Length > MaxValueLength) return (category, value, $"Value is limited to {MaxValueLength} characters.");
        return (category, value, null);
    }

    // Unknown categories become blank so Upsert counts the row as skipped; sort order and active flag are
    // written in the same form Values() reads them back, so unchanged rows compare equal.
    internal static string?[] NormalizeRow(string?[] row)
    {
        var category = Categories.Contains(row[0]) ? row[0] : null;
        _ = int.TryParse(row[2], out var sortOrder);
        var active = (row[3] ?? "").ToLowerInvariant() is "" or "1" or "true" or "yes" or "y";
        return [category, row[1], sortOrder.ToString(CultureInfo.InvariantCulture), active ? "1" : "0"];
    }

    private static string?[] Values(LookupItem l)
        => [l.Category, l.Value, l.SortOrder.ToString(CultureInfo.InvariantCulture), l.IsActive ? "1" : "0"];

    private static void Write(LookupItem l, string?[] v)
    {
        l.Category = v[0]!;
        l.Value = Clean(v[1])!;
        l.SortOrder = int.Parse(v[2]!, CultureInfo.InvariantCulture);
        l.IsActive = v[3] == "1";
    }

    private static LookupDto ToDto(LookupItem l) => new(l.Id, l.Category, l.Value, l.SortOrder, l.IsActive);

    private Task<List<LookupItem>> ItemsAsync(string? category, CancellationToken ct)
        => sp.EntitiesAsync<LookupItem>("SP_DCenter_Lookup_List", ct, Sql.NVarChar("@Category", category, 50));

    private Task<bool> IsDuplicateAsync(string category, string value, int? excludeId, CancellationToken ct)
        => sp.ScalarAsync<bool>("SP_DCenter_Lookup_IsDuplicate", ct,
            Sql.NVarChar("@Category", category, 50), Sql.NVarChar("@Value", value, MaxValueLength), Sql.Int("@ExcludeId", excludeId ?? 0));

    private static ServiceResult<T> Duplicate<T>(string category, string value)
        => ServiceResult<T>.Fail($"'{value}' already exists in {category}.", StatusCodes.Status409Conflict);

    // Updates the rows that have an Id, inserts the others in order, and returns the inserted rows.
    private Task<List<LookupItem>> SaveAsync(IEnumerable<LookupItem> updated, IEnumerable<LookupItem> added, CancellationToken ct)
    {
        var rows = new DataTable();
        rows.Columns.Add("Seq", typeof(int));
        rows.Columns.Add("Id", typeof(int));
        rows.Columns.Add("Category", typeof(string));
        rows.Columns.Add("Value", typeof(string));
        rows.Columns.Add("SortOrder", typeof(int));
        rows.Columns.Add("IsActive", typeof(bool));
        foreach (var l in updated) rows.Rows.Add(rows.Rows.Count, l.Id, l.Category, l.Value, l.SortOrder, l.IsActive);
        foreach (var l in added) rows.Rows.Add(rows.Rows.Count, DBNull.Value, l.Category, l.Value, l.SortOrder, l.IsActive);
        return StoredProcedures.Write(sp.EntitiesAsync<LookupItem>("SP_DCenter_Lookup_Save", ct, Sql.Table("@Rows", "dbo.TT_DCenter_LookupRows", rows)));
    }

    // Null when the category/value pair already exists.
    private async Task<List<LookupItem>?> TrySaveAsync(IEnumerable<LookupItem> updated, IEnumerable<LookupItem> added, CancellationToken ct)
    {
        try
        {
            return await SaveAsync(updated, added, ct);
        }
        catch (DbUpdateException ex) when (ReportSaveRules.IsDuplicateKey(ex))
        {
            return null;
        }
    }
}
