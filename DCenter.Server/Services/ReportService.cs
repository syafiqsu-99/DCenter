using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class ReportService(WeldReportContext db)
{
    public async Task<Report?> GetEntityAsync(string jobNumber, CancellationToken ct)
        => await db.Reports
            .Include(r => r.Joints).ThenInclude(j => j.Materials)
            .FirstOrDefaultAsync(r => r.JobNumber == jobNumber, ct);

    public async Task<ReportDto?> LoadAsync(string jobNumber, CancellationToken ct)
    {
        var r = await GetEntityAsync(jobNumber, ct);
        return r is null ? null : ToDto(r);
    }

    // All saved reports for the list under the job table, newest first.
    public async Task<List<ReportSummary>> ListAsync(CancellationToken ct)
        => await db.Reports
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new ReportSummary(
                r.Id,
                r.JobNumber,
                r.PartNo,
                r.Description,
                r.Joints.Count,
                r.CompletedAt == null ? "Draft" : "Completed",
                r.UpdatedAt))
            .ToListAsync(ct);

    public enum CompleteResult { Ok, NotFound, DateWeldedRequired }

    public async Task<CompleteResult> MarkCompleteAsync(string jobNumber, bool complete, CancellationToken ct)
    {
        var r = await db.Reports.FirstOrDefaultAsync(x => x.JobNumber == jobNumber, ct);
        if (r is null) return CompleteResult.NotFound;
        if (complete && r.DateWelded is null) return CompleteResult.DateWeldedRequired;
        r.CompletedAt = complete ? DateTime.UtcNow : null;
        r.UpdatedAt = DateTime.UtcNow;
        db.ReportStatusEvents.Add(new ReportStatusEvent
        {
            ReportId = r.Id,
            Action = complete ? "Completed" : "Reopened",
        });
        await db.SaveChangesAsync(ct);
        return CompleteResult.Ok;
    }

    // Newest first, for the history panel on the report actions bar.
    public async Task<List<ReportStatusEventDto>?> GetHistoryAsync(string jobNumber, CancellationToken ct)
    {
        var reportId = await db.Reports
            .Where(x => x.JobNumber == jobNumber)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (reportId is null) return null;

        return await db.ReportStatusEvents
            .Where(e => e.ReportId == reportId)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => new ReportStatusEventDto(e.Action, e.OccurredAt))
            .ToListAsync(ct);
    }

    public enum DeleteResult { Ok, NotFound, Completed }

    // Drafts only: a completed report must be reopened before it can be deleted.
    public async Task<DeleteResult> DeleteAsync(string jobNumber, CancellationToken ct)
    {
        var r = await db.Reports.FirstOrDefaultAsync(x => x.JobNumber == jobNumber, ct);
        if (r is null) return DeleteResult.NotFound;
        if (r.CompletedAt is not null) return DeleteResult.Completed;
        db.Reports.Remove(r);
        await db.SaveChangesAsync(ct);
        return DeleteResult.Ok;
    }

    // Upsert the whole draft graph keyed on job number.
    public async Task<ReportDto> SaveAsync(ReportDto dto, CancellationToken ct)
    {
        var r = await db.Reports
            .Include(x => x.Joints).ThenInclude(j => j.Materials)
            .FirstOrDefaultAsync(x => x.JobNumber == dto.JobNumber, ct);

        var isInsert = r is null;

        if (r is null)
        {
            r = new Report { JobNumber = dto.JobNumber, CreatedAt = DateTime.UtcNow };
            db.Reports.Add(r);
        }
        else
        {
            if (!string.IsNullOrEmpty(dto.RowVersion))
            {
                db.Entry(r).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
            }
            db.JointMaterials.RemoveRange(r.Joints.SelectMany(j => j.Materials));
            db.Joints.RemoveRange(r.Joints);
            r.Joints.Clear();
        }

        ApplyHeader(r, dto);
        r.UpdatedAt = DateTime.UtcNow;

        foreach (var jd in dto.Joints.OrderBy(j => j.JointNumber).Take(9))
        {
            var joint = new Joint
            {
                JointNumber = jd.JointNumber,
                PartDescLeft = jd.PartDescLeft,
                PartNoLeft = jd.PartNoLeft,
                HeatNumberLeft = jd.HeatNumberLeft,
                PartDescRight = jd.PartDescRight,
                PartNoRight = jd.PartNoRight,
                HeatNumberRight = jd.HeatNumberRight,
                WpsNo = jd.WpsNo,
                Rev = jd.Rev,
                WelderName = jd.WelderName,
                WelderNo = jd.WelderNo,
                Materials = jd.Materials
                    .Where(m => m.ColumnNumber is >= 1 and <= 3)
                    .Select(m => new JointMaterial
                    {
                        ColumnNumber = m.ColumnNumber,
                        Process = m.Process,
                        Size = m.Size,
                        Type = m.Type,
                        Manuf = m.Manuf,
                        HeatLot = m.HeatLot
                    }).ToList()
            };
            r.Joints.Add(joint);
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReportConflictException(
                "This report was changed by someone else since you loaded it. Reload the report and re-apply your changes.");
        }
        catch (DbUpdateException) when (isInsert)
        {
            throw new ReportConflictException(
                $"A report for job {dto.JobNumber} was just created by someone else. Reload the list and open it.");
        }

        return ToDto(r);
    }

    private static void ApplyHeader(Report r, ReportDto d)
    {
        r.ReportRequired = d.ReportRequired;
        r.DateWelded = d.DateWelded;
        r.WorkOrder = d.WorkOrder;
        r.PartNo = d.PartNo;
        r.Description = d.Description;
        r.MaterialSpec1 = d.MaterialSpec1; r.MaterialSpec2 = d.MaterialSpec2; r.MaterialSpec3 = d.MaterialSpec3;
        r.Grade1 = d.Grade1; r.Grade2 = d.Grade2; r.Grade3 = d.Grade3;
        r.PNumber1 = d.PNumber1; r.PNumber2 = d.PNumber2; r.PNumber3 = d.PNumber3;
        r.EngineerSupervisor = d.EngineerSupervisor;
        r.QaInspector = d.QaInspector;
    }

    public static ReportDto ToDto(Report r) => new()
    {
        Id = r.Id,
        JobNumber = r.JobNumber,
        ReportRequired = r.ReportRequired,
        DateWelded = r.DateWelded,
        WorkOrder = r.WorkOrder,
        PartNo = r.PartNo,
        Description = r.Description,
        MaterialSpec1 = r.MaterialSpec1,
        MaterialSpec2 = r.MaterialSpec2,
        MaterialSpec3 = r.MaterialSpec3,
        Grade1 = r.Grade1,
        Grade2 = r.Grade2,
        Grade3 = r.Grade3,
        PNumber1 = r.PNumber1,
        PNumber2 = r.PNumber2,
        PNumber3 = r.PNumber3,
        EngineerSupervisor = r.EngineerSupervisor,
        QaInspector = r.QaInspector,
        CompletedAt = r.CompletedAt,
        RowVersion = r.RowVersion is { Length: > 0 } ? Convert.ToBase64String(r.RowVersion) : null,
        Joints = r.Joints.OrderBy(j => j.JointNumber).Select(j => new JointDto
        {
            Id = j.Id,
            JointNumber = j.JointNumber,
            PartDescLeft = j.PartDescLeft,
            PartNoLeft = j.PartNoLeft,
            HeatNumberLeft = j.HeatNumberLeft,
            PartDescRight = j.PartDescRight,
            PartNoRight = j.PartNoRight,
            HeatNumberRight = j.HeatNumberRight,
            WpsNo = j.WpsNo,
            Rev = j.Rev,
            WelderName = j.WelderName,
            WelderNo = j.WelderNo,
            Materials = j.Materials.OrderBy(m => m.ColumnNumber).Select(m => new JointMaterialDto
            {
                Id = m.Id,
                ColumnNumber = m.ColumnNumber,
                Process = m.Process,
                Size = m.Size,
                Type = m.Type,
                Manuf = m.Manuf,
                HeatLot = m.HeatLot
            }).ToList()
        }).ToList()
    };
}