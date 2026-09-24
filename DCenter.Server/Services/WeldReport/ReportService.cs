using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class ReportService(WeldReportContext db)
{
    public async Task<Report?> GetEntityAsync(string workOrderNumber, CancellationToken ct)
        => await db.Reports
            .Include(r => r.Joints).ThenInclude(j => j.Materials)
            .FirstOrDefaultAsync(r => r.WorkOrderNumber == workOrderNumber, ct);

    public async Task<ReportDto?> LoadAsync(string workOrderNumber, CancellationToken ct)
    {
        var r = await GetEntityAsync(workOrderNumber, ct);
        return r is null ? null : ToDto(r);
    }

    public async Task<List<ReportSummary>> ListAsync(CancellationToken ct)
        => await db.Reports
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new ReportSummary(
                r.Id,
                r.WorkOrderNumber,
                r.PartNo,
                r.Description,
                r.Joints.Count,
                r.CompletedAt == null ? "Draft" : "Completed",
                r.UpdatedAt))
            .ToListAsync(ct);

    public enum CompleteResult { Ok, NotFound, DateWeldedRequired }

    public async Task<CompleteResult> MarkCompleteAsync(string workOrderNumber, bool complete, CancellationToken ct)
    {
        var r = await db.Reports.FirstOrDefaultAsync(x => x.WorkOrderNumber == workOrderNumber, ct);
        if (r is null) return CompleteResult.NotFound;
        if (complete && r.DateWelded is null) return CompleteResult.DateWeldedRequired;
        r.CompletedAt = complete ? DateTime.Now : null;
        r.UpdatedAt = DateTime.Now;
        db.ReportStatusEvents.Add(new ReportStatusEvent
        {
            ReportId = r.Id,
            Action = complete ? "Completed" : "Reopened",
        });
        await db.SaveChangesAsync(ct);
        return CompleteResult.Ok;
    }

    public async Task<List<ReportStatusEventDto>?> GetHistoryAsync(string WorkOrderNumber, CancellationToken ct)
    {
        var reportId = await db.Reports
            .Where(x => x.WorkOrderNumber == WorkOrderNumber)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct);
        if (reportId is null) return null;

        return await db.ReportStatusEvents
            .Where(e => e.ReportId == reportId)
            .OrderByDescending(e => e.OccurredAt)
            .Select(e => new ReportStatusEventDto(e.Action, e.OccurredAt, e.Details))
            .ToListAsync(ct);
    }

    public enum DeleteResult { Ok, NotFound, Completed }

    public async Task<DeleteResult> DeleteAsync(string WorkOrderNumber, CancellationToken ct)
    {
        var r = await db.Reports.FirstOrDefaultAsync(x => x.WorkOrderNumber == WorkOrderNumber, ct);
        if (r is null) return DeleteResult.NotFound;
        if (r.CompletedAt is not null) return DeleteResult.Completed;
        db.Reports.Remove(r);
        await db.SaveChangesAsync(ct);
        return DeleteResult.Ok;
    }

    public async Task<ReportDto> SaveAsync(ReportDto dto, CancellationToken ct)
    {
        var r = await db.Reports
            .Include(x => x.Joints).ThenInclude(j => j.Materials)
            .FirstOrDefaultAsync(x => x.WorkOrderNumber == dto.WorkOrderNumber, ct);

        var isInsert = r is null;

        string summary;
        if (r is null)
        {
            r = new Report { WorkOrderNumber = dto.WorkOrderNumber, CreatedAt = DateTime.Now };
            db.Reports.Add(r);
            summary = "Report created";
        }
        else
        {
            summary = BuildSaveSummary(r, dto);
            if (!string.IsNullOrEmpty(dto.RowVersion))
            {
                db.Entry(r).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(dto.RowVersion);
            }
            db.JointMaterials.RemoveRange(r.Joints.SelectMany(j => j.Materials));
            db.Joints.RemoveRange(r.Joints);
            r.Joints.Clear();
        }

        ApplyHeader(r, dto);
        r.UpdatedAt = DateTime.Now;

        foreach (var jd in dto.Joints.OrderBy(j => j.JointNumber).Take(50))
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

        db.ReportStatusEvents.Add(new ReportStatusEvent
        {
            Report = r,
            Action = isInsert ? "Created" : "Saved",
            Details = summary,
        });

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
                $"A report for job {dto.WorkOrderNumber} was just created by someone else. Reload the list and open it.");
        }

        return ToDto(r);
    }

    private static void ApplyHeader(Report r, ReportDto d)
    {
        r.ReportRequired = d.ReportRequired;
        r.DateWelded = d.DateWelded;
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
        WorkOrderNumber = r.WorkOrderNumber,
        ReportRequired = r.ReportRequired,
        DateWelded = r.DateWelded,
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

    private static string BuildSaveSummary(Report old, ReportDto dto)
    {
        var changes = new List<string>();
        void Cmp(string label, string? a, string? b)
        {
            if ((a ?? "") != (b ?? "")) changes.Add(label);
        }

        Cmp("Date Welded", old.DateWelded?.ToString("yyyy-MM-dd"), dto.DateWelded?.ToString("yyyy-MM-dd"));
        if (old.ReportRequired != dto.ReportRequired) changes.Add("Report Required");
        Cmp("Part No.", old.PartNo, dto.PartNo);
        Cmp("Description", old.Description, dto.Description);
        Cmp("Material 1", old.MaterialSpec1, dto.MaterialSpec1);
        Cmp("Material 2", old.MaterialSpec2, dto.MaterialSpec2);
        Cmp("Material 3", old.MaterialSpec3, dto.MaterialSpec3);
        Cmp("Grade 1", old.Grade1, dto.Grade1);
        Cmp("Grade 2", old.Grade2, dto.Grade2);
        Cmp("Grade 3", old.Grade3, dto.Grade3);
        Cmp("P# 1", old.PNumber1, dto.PNumber1);
        Cmp("P# 2", old.PNumber2, dto.PNumber2);
        Cmp("P# 3", old.PNumber3, dto.PNumber3);
        Cmp("Engineer/Supervisor", old.EngineerSupervisor, dto.EngineerSupervisor);
        Cmp("QA Inspector", old.QaInspector, dto.QaInspector);
        if (JointSignature(old.Joints) != JointSignature(dto.Joints)) changes.Add("Joints");

        return changes.Count == 0 ? "Saved with no field changes" : "Updated: " + string.Join(", ", changes);
    }

    private static string JointSignature(IEnumerable<Joint> joints) =>
        string.Join("|", joints.OrderBy(j => j.JointNumber).Select(j =>
            $"{j.JointNumber};{j.PartDescLeft};{j.PartNoLeft};{j.HeatNumberLeft};{j.PartDescRight};{j.PartNoRight};{j.HeatNumberRight};{j.WpsNo};{j.Rev};{j.WelderName};{j.WelderNo};" +
            string.Join(",", j.Materials.OrderBy(m => m.ColumnNumber)
                .Select(m => $"{m.ColumnNumber}:{m.Process}:{m.Size}:{m.Type}:{m.Manuf}:{m.HeatLot}"))));

    private static string JointSignature(IEnumerable<JointDto> joints) =>
        string.Join("|", joints.OrderBy(j => j.JointNumber).Select(j =>
            $"{j.JointNumber};{j.PartDescLeft};{j.PartNoLeft};{j.HeatNumberLeft};{j.PartDescRight};{j.PartNoRight};{j.HeatNumberRight};{j.WpsNo};{j.Rev};{j.WelderName};{j.WelderNo};" +
            string.Join(",", j.Materials.OrderBy(m => m.ColumnNumber)
                .Select(m => $"{m.ColumnNumber}:{m.Process}:{m.Size}:{m.Type}:{m.Manuf}:{m.HeatLot}"))));
}