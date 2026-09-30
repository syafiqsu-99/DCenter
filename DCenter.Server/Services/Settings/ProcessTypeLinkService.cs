using DCenter.Server.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class ProcessTypeLinkService(StoredProcedures sp)
{
    public const int MaxLength = 200;
    private const string Exists = "That Process–Type link already exists.";

    public Task<List<ProcessTypeLinkDto>> ListAsync(CancellationToken ct)
        => sp.QueryAsync<ProcessTypeLinkDto>("SP_DCenter_ProcessTypeLink_List", ct);

    public async Task<ServiceResult<ProcessTypeLinkDto>> CreateAsync(ProcessTypeLinkUpsert dto, CancellationToken ct)
    {
        var process = (dto.Process ?? string.Empty).Trim();
        var type = (dto.Type ?? string.Empty).Trim();
        if (process.Length == 0 || type.Length == 0)
            return ServiceResult<ProcessTypeLinkDto>.Fail("Process and Type are both required.");
        if (process.Length > MaxLength || type.Length > MaxLength)
            return ServiceResult<ProcessTypeLinkDto>.Fail($"Process and Type are limited to {MaxLength} characters.");

        if ((await sp.QueryAsync<ProcessTypeLinkDto>("SP_DCenter_ProcessTypeLink_List", ct, Key(process, type))).Count > 0)
            return ServiceResult<ProcessTypeLinkDto>.Fail(Exists, StatusCodes.Status409Conflict);

        try
        {
            var link = await StoredProcedures.Write(sp.QueryAsync<ProcessTypeLinkDto>("SP_DCenter_ProcessTypeLink_Insert", ct, Key(process, type)));
            return ServiceResult<ProcessTypeLinkDto>.Ok(link.Single());
        }
        catch (DbUpdateException ex) when (ReportSaveRules.IsDuplicateKey(ex))
        {
            return ServiceResult<ProcessTypeLinkDto>.Fail(Exists, StatusCodes.Status409Conflict);
        }
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var deleted = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_DCenter_ProcessTypeLink_Delete", ct, Sql.Int("@Id", id)));
        return deleted == 0 ? ServiceResult<bool>.NotFound() : ServiceResult<bool>.Ok(true);
    }

    private static SqlParameter[] Key(string process, string type)
        => [Sql.NVarChar("@Process", process, MaxLength), Sql.NVarChar("@Type", type, MaxLength)];
}
