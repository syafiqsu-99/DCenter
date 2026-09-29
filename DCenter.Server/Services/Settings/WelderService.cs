using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class WelderService(WeldReportContext db, ILogger<WelderService> logger)
{
    public const int SearchLimit = 20;

    public Task<List<WelderDto>> ListAsync(CancellationToken ct)
        => Project(db.Welders.AsNoTracking().OrderBy(w => w.WelderName)).ToListAsync(ct);

    public Task<List<WelderDto>> SearchAsync(string? q, bool stockOnly, CancellationToken ct)
    {
        q = (q ?? string.Empty).Trim();
        var query = db.Welders.AsNoTracking().Where(w => w.IsActive);
        if (stockOnly) query = query.Where(w => w.UsageScope == WelderScope.ReportAndStock);
        if (q.Length > 0) query = query.Where(w => w.WelderName.Contains(q) || w.WelderNo.Contains(q));
        return Project(query.OrderBy(w => w.WelderName).Take(SearchLimit)).ToListAsync(ct);
    }

    public async Task<ServiceResult<WelderDto>> CreateAsync(WelderDto dto, CancellationToken ct)
    {
        var (name, number, scope, error) = Validate(dto);
        if (error is not null) return ServiceResult<WelderDto>.Fail(error);
        if (await DuplicateAsync(number!, 0, ct) is string duplicate) return ServiceResult<WelderDto>.Fail(duplicate, StatusCodes.Status409Conflict);

        var w = new Welder { WelderName = name!, WelderNo = number!, IsActive = dto.IsActive, UsageScope = scope! };
        db.Welders.Add(w);
        if (await SaveAsync(ct) is string conflict) return ServiceResult<WelderDto>.Fail(conflict, StatusCodes.Status409Conflict);
        return ServiceResult<WelderDto>.Ok(ToDto(w));
    }

    public async Task<ServiceResult<bool>> UpdateAsync(int id, WelderDto dto, CancellationToken ct)
    {
        var (name, number, scope, error) = Validate(dto);
        if (error is not null) return ServiceResult<bool>.Fail(error);

        var w = await db.Welders.FindAsync([id], ct);
        if (w is null) return ServiceResult<bool>.NotFound();
        if (await DuplicateAsync(number!, id, ct) is string duplicate) return ServiceResult<bool>.Fail(duplicate, StatusCodes.Status409Conflict);

        (w.WelderName, w.WelderNo, w.IsActive, w.UsageScope) = (name!, number!, dto.IsActive, scope!);
        if (await SaveAsync(ct) is string conflict) return ServiceResult<bool>.Fail(conflict, StatusCodes.Status409Conflict);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<int>> SetScopeAsync(WelderScopeUpdate dto, CancellationToken ct)
    {
        var scope = WelderScope.Normalize(dto.UsageScope);
        if (scope is null) return ServiceResult<int>.Fail("Scope must be Report or ReportAndStock.");
        var ids = (dto.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0) return ServiceResult<int>.Fail("Select at least one welder.");

        var updated = await db.Welders
            .Where(w => ids.Contains(w.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.UsageScope, scope), ct);
        return ServiceResult<int>.Ok(updated);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var w = await db.Welders.FindAsync([id], ct);
        if (w is null) return ServiceResult<bool>.NotFound();
        var inUse = $"{w.WelderName} has consumable pickups or returns on record. Set the welder inactive instead.";
        if (await db.ConsumableMovements.AnyAsync(m => m.WelderId == id, ct) || await db.HoldingRecords.AnyAsync(h => h.WelderId == id, ct))
            return ServiceResult<bool>.Fail(inUse, StatusCodes.Status409Conflict);

        db.Welders.Remove(w);
        if (await SaveAsync(ct) is not null) return ServiceResult<bool>.Fail(inUse, StatusCodes.Status409Conflict);
        return ServiceResult<bool>.Ok(true);
    }

    internal static string? DuplicateNumberError(string number, string? registeredTo)
        => number.Length == 0 || registeredTo is null
            ? null
            : $"Welder No. {number} is already registered to {registeredTo}. Pick that name from the list instead.";

    private async Task<string?> DuplicateAsync(string number, int excludeId, CancellationToken ct)
    {
        var holder = number.Length == 0
            ? null
            : await db.Welders.AsNoTracking().Where(x => x.WelderNo == number && x.Id != excludeId)
                .Select(x => x.WelderName).FirstOrDefaultAsync(ct);
        return DuplicateNumberError(number, holder);
    }

    private async Task<string?> SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return null;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Welder save failed");
            return "This change could not be saved because the welder list changed at the same time. Reload and try again.";
        }
    }

    private static WelderDto ToDto(Welder w) => new(w.Id, w.WelderName, w.WelderNo, w.IsActive, w.UsageScope);

    private static IQueryable<WelderDto> Project(IQueryable<Welder> q)
        => q.Select(w => new WelderDto(w.Id, w.WelderName, w.WelderNo, w.IsActive, w.UsageScope));

    private static (string? Name, string? Number, string? Scope, string? Error) Validate(WelderDto dto)
    {
        var name = dto.WelderName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200) return (null, null, null, "Welder Name is required (max 200 characters).");
        var number = dto.WelderNo?.Trim() ?? string.Empty;
        if (number.Length > 50) return (null, null, null, "Welder No. is limited to 50 characters.");
        var scope = WelderScope.Normalize(dto.UsageScope ?? WelderScope.Report);
        if (scope is null) return (null, null, null, "Scope must be Report or ReportAndStock.");
        return (name, number, scope, null);
    }
}
