using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class ProcessTypeLinkService(WeldReportContext db)
{
    public const int MaxLength = 200;
    private const string Exists = "That Process–Type link already exists.";

    public Task<List<ProcessTypeLinkDto>> ListAsync(CancellationToken ct)
        => db.ProcessTypeLinks.AsNoTracking().OrderBy(x => x.Process).ThenBy(x => x.Type)
            .Select(x => new ProcessTypeLinkDto(x.Id, x.Process, x.Type))
            .ToListAsync(ct);

    public async Task<ServiceResult<ProcessTypeLinkDto>> CreateAsync(ProcessTypeLinkUpsert dto, CancellationToken ct)
    {
        var process = (dto.Process ?? string.Empty).Trim();
        var type = (dto.Type ?? string.Empty).Trim();
        if (process.Length == 0 || type.Length == 0)
            return ServiceResult<ProcessTypeLinkDto>.Fail("Process and Type are both required.");
        if (process.Length > MaxLength || type.Length > MaxLength)
            return ServiceResult<ProcessTypeLinkDto>.Fail($"Process and Type are limited to {MaxLength} characters.");

        if (await db.ProcessTypeLinks.AnyAsync(x => x.Process == process && x.Type == type, ct))
            return ServiceResult<ProcessTypeLinkDto>.Fail(Exists, StatusCodes.Status409Conflict);

        var link = new ProcessTypeLink { Process = process, Type = type };
        db.ProcessTypeLinks.Add(link);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ReportSaveRules.IsDuplicateKey(ex))
        {
            return ServiceResult<ProcessTypeLinkDto>.Fail(Exists, StatusCodes.Status409Conflict);
        }
        return ServiceResult<ProcessTypeLinkDto>.Ok(new ProcessTypeLinkDto(link.Id, link.Process, link.Type));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        var link = await db.ProcessTypeLinks.FindAsync([id], ct);
        if (link is null) return ServiceResult<bool>.NotFound();
        db.ProcessTypeLinks.Remove(link);
        await db.SaveChangesAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }
}
