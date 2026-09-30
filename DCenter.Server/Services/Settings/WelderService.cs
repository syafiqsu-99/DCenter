using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class WelderService(StoredProcedures sp, ILogger<WelderService> logger)
{
    public const int SearchLimit = 20;

    public Task<List<WelderDto>> ListAsync(CancellationToken ct)
        => sp.QueryAsync<WelderDto>("SP_DCenter_Welder_List", ct);

    public Task<List<WelderDto>> SearchAsync(string? q, bool stockOnly, CancellationToken ct)
        => sp.QueryAsync<WelderDto>("SP_DCenter_Welder_List", ct,
            Sql.NVarChar("@Q", (q ?? string.Empty).Trim() is { Length: > 0 } term ? term : null, 4000),
            Sql.Bit("@ActiveOnly", true),
            Sql.Bit("@StockOnly", stockOnly),
            Sql.Int("@Take", SearchLimit));

    public async Task<ServiceResult<WelderDto>> CreateAsync(WelderDto dto, CancellationToken ct)
    {
        var (name, number, scope, error) = Validate(dto);
        if (error is not null) return ServiceResult<WelderDto>.Fail(error);
        if (await DuplicateAsync(number!, 0, ct) is string duplicate) return ServiceResult<WelderDto>.Fail(duplicate, StatusCodes.Status409Conflict);

        WelderDto? created = null;
        if (!await TrySaveAsync(async () => created = (await sp.QueryAsync<WelderDto>("SP_DCenter_Welder_Save", ct, [Sql.Int("@Id", null), .. Fields(name!, number!, dto.IsActive, scope!)])).Single()))
            return ServiceResult<WelderDto>.Fail(ConflictMessage, StatusCodes.Status409Conflict);
        return ServiceResult<WelderDto>.Ok(created!);
    }

    public async Task<ServiceResult<bool>> UpdateAsync(int id, WelderDto dto, CancellationToken ct)
    {
        var (name, number, scope, error) = Validate(dto);
        if (error is not null) return ServiceResult<bool>.Fail(error);

        if (await GetAsync(id, ct) is null) return ServiceResult<bool>.NotFound();
        if (await DuplicateAsync(number!, id, ct) is string duplicate) return ServiceResult<bool>.Fail(duplicate, StatusCodes.Status409Conflict);

        if (!await TrySaveAsync(() => sp.QueryAsync<WelderDto>("SP_DCenter_Welder_Save", ct, [Sql.Int("@Id", id), .. Fields(name!, number!, dto.IsActive, scope!)])))
            return ServiceResult<bool>.Fail(ConflictMessage, StatusCodes.Status409Conflict);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<int>> SetScopeAsync(WelderScopeUpdate dto, CancellationToken ct)
    {
        var scope = WelderScope.Normalize(dto.UsageScope);
        if (scope is null) return ServiceResult<int>.Fail("Scope must be Report or ReportAndStock.");
        var ids = (dto.Ids ?? []).Distinct().ToList();
        if (ids.Count == 0) return ServiceResult<int>.Fail("Select at least one welder.");

        var updated = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_DCenter_Welder_SetScope", ct,
            Sql.IdList("@Ids", ids), Sql.NVarChar("@UsageScope", scope, 20)));
        return ServiceResult<int>.Ok(updated);
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var w = await GetAsync(id, ct);
        if (w is null) return ServiceResult<bool>.NotFound();
        var inUse = $"{w.WelderName} has consumable pickups or returns on record. Set the welder inactive instead.";

        var deleted = false;
        if (!await TrySaveAsync(async () => deleted = await sp.ScalarAsync<bool>("SP_DCenter_Welder_Delete", ct, Sql.Int("@Id", id))) || !deleted)
            return ServiceResult<bool>.Fail(inUse, StatusCodes.Status409Conflict);
        return ServiceResult<bool>.Ok(true);
    }

    internal static string? DuplicateNumberError(string number, string? registeredTo)
        => number.Length == 0 || registeredTo is null
            ? null
            : $"Welder No. {number} is already registered to {registeredTo}. Pick that name from the list instead.";

    private const string ConflictMessage =
        "This change could not be saved because the welder list changed at the same time. Reload and try again.";

    private Task<WelderDto?> GetAsync(int id, CancellationToken ct)
        => sp.FirstOrDefaultAsync<WelderDto>("SP_DCenter_Welder_List", ct, Sql.Int("@Id", id));

    private async Task<string?> DuplicateAsync(string number, int excludeId, CancellationToken ct)
    {
        var holder = number.Length == 0
            ? null
            : (await sp.FirstOrDefaultAsync<WelderDto>("SP_DCenter_Welder_List", ct,
                Sql.NVarChar("@WelderNo", number, 50), Sql.Int("@ExcludeId", excludeId), Sql.Int("@Take", 1)))?.WelderName;
        return DuplicateNumberError(number, holder);
    }

    private static SqlParameter[] Fields(string name, string number, bool isActive, string scope) =>
    [
        Sql.NVarChar("@WelderName", name, 200),
        Sql.NVarChar("@WelderNo", number, 50),
        Sql.Bit("@IsActive", isActive),
        Sql.NVarChar("@UsageScope", scope, 20),
    ];

    // False when the database refused the write (logged); the caller answers 409.
    private async Task<bool> TrySaveAsync<T>(Func<Task<T>> write)
    {
        try
        {
            await StoredProcedures.Write(write());
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex, "Welder save failed");
            return false;
        }
    }

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
