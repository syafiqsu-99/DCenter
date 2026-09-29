using System.Globalization;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Services;

public class LookupService(WeldReportContext db, ILogger<LookupService> logger)
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

    public Task<List<LookupDto>> ListAsync(string? category, CancellationToken ct)
    {
        var query = db.Lookups.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(l => l.Category == category);
        return Ordered(query)
            .Select(l => new LookupDto(l.Id, l.Category, l.Value, l.SortOrder, l.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<LookupDto>> CreateAsync(LookupUpsert dto, CancellationToken ct)
    {
        var (category, value, error) = Validate(dto);
        if (error is not null) return ServiceResult<LookupDto>.Fail(error);
        if (await IsDuplicateAsync(category, value, null, ct)) return Duplicate<LookupDto>(category, value);

        var l = new LookupItem { Category = category, Value = value, SortOrder = dto.SortOrder, IsActive = dto.IsActive };
        db.Lookups.Add(l);
        if (!await TrySaveAsync(ct)) return Duplicate<LookupDto>(category, value);
        return ServiceResult<LookupDto>.Ok(new LookupDto(l.Id, l.Category, l.Value, l.SortOrder, l.IsActive));
    }

    public async Task<ServiceResult<bool>> UpdateAsync(int id, LookupUpsert dto, CancellationToken ct)
    {
        var (category, value, error) = Validate(dto);
        if (error is not null) return ServiceResult<bool>.Fail(error);

        var l = await db.Lookups.FindAsync([id], ct);
        if (l is null) return ServiceResult<bool>.NotFound();
        if (await IsDuplicateAsync(category, value, id, ct)) return Duplicate<bool>(category, value);

        (l.Category, l.Value, l.SortOrder, l.IsActive) = (category, value, dto.SortOrder, dto.IsActive);
        if (!await TrySaveAsync(ct)) return Duplicate<bool>(category, value);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task ReorderAsync(List<int> ids, CancellationToken ct)
    {
        var items = await db.Lookups.Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        foreach (var l in items) l.SortOrder = ids.IndexOf(l.Id);
        await db.SaveChangesAsync(ct);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var l = await db.Lookups.FindAsync([id], ct);
        if (l is null) return ServiceResult<bool>.NotFound();
        db.Lookups.Remove(l);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<byte[]> ExportAsync(CancellationToken ct)
        => Export(Columns, (await Ordered(db.Lookups.AsNoTracking()).ToListAsync(ct)).Select(Values));

    public async Task<ServiceResult<ImportCounts>> ImportAsync(IFormFile? file, CancellationToken ct)
    {
        var sheet = await ReadAsync(file, Columns, ct);
        if (sheet.Errors.Count > 0) return ServiceResult<ImportCounts>.Fail(string.Join(" ", sheet.Errors));

        var existing = await db.Lookups.ToListAsync(ct);
        var counts = Upsert(Columns, sheet.Rows.Select(NormalizeRow), existing, Values, Write, () =>
        {
            var l = new LookupItem();
            db.Lookups.Add(l);
            return l;
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Dropdown list import failed to save");
            return ServiceResult<ImportCounts>.Fail(ImportSaveConflict, StatusCodes.Status409Conflict);
        }
        return ServiceResult<ImportCounts>.Ok(counts);
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

    private static IQueryable<LookupItem> Ordered(IQueryable<LookupItem> q)
        => q.OrderBy(l => l.Category).ThenBy(l => l.SortOrder).ThenBy(l => l.Value);

    private Task<bool> IsDuplicateAsync(string category, string value, int? excludeId, CancellationToken ct)
        => db.Lookups.AnyAsync(l => l.Category == category && l.Value == value && l.Id != (excludeId ?? 0), ct);

    private static ServiceResult<T> Duplicate<T>(string category, string value)
        => ServiceResult<T>.Fail($"'{value}' already exists in {category}.", StatusCodes.Status409Conflict);

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ReportSaveRules.IsDuplicateKey(ex))
        {
            return false;
        }
    }
}
