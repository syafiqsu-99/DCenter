using System.Text.Json;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class ReportService(StoredProcedures sp, TimeProvider time)
{
    // The report with its joints and electrode data, read in one statement so they come from one version.
    public async Task<Report?> GetEntityAsync(string workOrderNumber, CancellationToken ct)
    {
        var json = await sp.ScalarAsync<string?>("SP_Report_Get", ct,
            Sql.NVarChar("@WorkOrderNumber", workOrderNumber, ReportSaveRules.MaxWorkOrderLength));
        return json is null ? null : JsonSerializer.Deserialize<Report>(json, Sql.JsonOptions);
    }

    public async Task<ReportDto?> LoadAsync(string workOrderNumber, CancellationToken ct)
    {
        var r = await GetEntityAsync(workOrderNumber, ct);
        return r is null ? null : ToDto(r);
    }

    public Task<List<ReportSummary>> ListAsync(CancellationToken ct)
        => sp.QueryAsync<ReportSummary>("SP_Report_List", ct);

    public enum CompleteResult { Ok, NotFound, DateWeldedRequired, Incomplete }

    public sealed record CompleteOutcome(CompleteResult Result, IReadOnlyList<string> Problems);

    public async Task<CompleteOutcome> MarkCompleteAsync(string workOrderNumber, bool complete, string? by, CancellationToken ct)
    {
        var r = await GetEntityAsync(workOrderNumber, ct);
        if (r is null) return new(CompleteResult.NotFound, []);
        if (complete && r.DateWelded is null) return new(CompleteResult.DateWeldedRequired, []);
        if (complete && ReportSaveRules.CompletionProblems(r) is { Count: > 0 } problems)
            return new(CompleteResult.Incomplete, problems);

        var now = time.LocalNow();
        await StoredProcedures.Write(sp.ExecuteAsync("SP_Report_SetStatus", ct,
            Sql.Int("@Id", r.Id),
            RowVersion(r.RowVersion),
            Sql.DateTime2("@CompletedAt", complete ? now : null),
            Sql.DateTime2("@UpdatedAt", now),
            Sql.NVarChar("@Action", complete ? ReportAction.Completed : ReportAction.Reopened, 50),
            Sql.NVarChar("@Details", by is null ? null : $"By {by}", 1000),
            Sql.DateTime2("@OccurredAt", now)));
        return new(CompleteResult.Ok, []);
    }

    public async Task<List<ReportStatusEventDto>?> GetHistoryAsync(string WorkOrderNumber, CancellationToken ct)
    {
        var r = await GetEntityAsync(WorkOrderNumber, ct);
        if (r is null) return null;
        return await sp.QueryAsync<ReportStatusEventDto>("SP_Report_History", ct, Sql.Int("@ReportId", r.Id));
    }

    public enum DeleteResult { Ok, NotFound, Completed }

    public async Task<DeleteResult> DeleteAsync(string WorkOrderNumber, CancellationToken ct)
    {
        var r = await GetEntityAsync(WorkOrderNumber, ct);
        if (r is null) return DeleteResult.NotFound;
        if (r.CompletedAt is not null) return DeleteResult.Completed;
        await StoredProcedures.Write(sp.ExecuteAsync("SP_Report_Delete", ct, Sql.Int("@Id", r.Id)));
        return DeleteResult.Ok;
    }

    public async Task<ReportDto> SaveAsync(ReportDto dto, CancellationToken ct)
    {
        var r = await GetEntityAsync(dto.WorkOrderNumber, ct);

        var isInsert = r is null;
        if (ReportSaveRules.Conflict(r, dto) is string refusal) throw new ReportConflictException(refusal);

        var now = time.LocalNow();
        var summary = r is null ? "Report created" : BuildSaveSummary(r, dto);
        var joints = ReportSaveRules.Renumber(dto.Joints).Take(ReportSaveRules.MaxJoints).ToList();

        try
        {
            await StoredProcedures.Write(sp.ScalarAsync<int>("SP_Report_Save", ct,
            [
                Sql.Int("@Id", r?.Id),
                RowVersion(r is null ? null : Convert.FromBase64String(dto.RowVersion!)),
                Sql.NVarChar("@WorkOrderNumber", dto.WorkOrderNumber, ReportSaveRules.MaxWorkOrderLength),
                .. HeaderParameters(dto),
                Sql.DateTime2("@CreatedAt", now),
                Sql.DateTime2("@UpdatedAt", now),
                Sql.NVarChar("@Action", isInsert ? ReportAction.Created : ReportAction.Saved, 50),
                Sql.NVarChar("@Details", summary, 1000),
                Sql.DateTime2("@OccurredAt", now),
                Sql.Json("@Joints", joints.Select((j, seq) => new
                {
                    Seq = seq, j.JointNumber, j.PartDescLeft, j.PartNoLeft, j.HeatNumberLeft, j.PartDescRight, j.PartNoRight,
                    j.HeatNumberRight, j.WpsNo, j.Rev, j.WelderName, j.WelderNo,
                })),
                Sql.Json("@Materials", joints.SelectMany((j, jointSeq) => j.Materials
                    .Where(m => m.ColumnNumber is >= 1 and <= 3)
                    .Select((m, seq) => new { JointSeq = jointSeq, Seq = seq, m.ColumnNumber, m.Process, m.Size, m.Type, m.Manuf, m.HeatLot }))),
            ]));
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ReportConflictException(
                "This report was changed by someone else since you loaded it. Reload the report and re-apply your changes.");
        }
        catch (DbUpdateException ex) when (isInsert && ReportSaveRules.IsDuplicateKey(ex))
        {
            throw new ReportConflictException(
                $"A report for work order {dto.WorkOrderNumber} was just created by someone else. Open it from Saved reports.");
        }

        return ToDto((await GetEntityAsync(dto.WorkOrderNumber, ct))!);
    }

    private static SqlParameter RowVersion(byte[]? value) => Sql.Binary("@RowVersion", value, 8);

    private static SqlParameter[] HeaderParameters(ReportDto d) =>
    [
        Sql.Bit("@ReportRequired", d.ReportRequired),
        Sql.Date("@DateWelded", d.DateWelded),
        Sql.NVarCharMax("@PartNo", d.PartNo),
        Sql.NVarCharMax("@Description", d.Description),
        Sql.NVarCharMax("@MaterialSpec1", d.MaterialSpec1),
        Sql.NVarCharMax("@MaterialSpec2", d.MaterialSpec2),
        Sql.NVarCharMax("@MaterialSpec3", d.MaterialSpec3),
        Sql.NVarCharMax("@Grade1", d.Grade1),
        Sql.NVarCharMax("@Grade2", d.Grade2),
        Sql.NVarCharMax("@Grade3", d.Grade3),
        Sql.NVarCharMax("@PNumber1", d.PNumber1),
        Sql.NVarCharMax("@PNumber2", d.PNumber2),
        Sql.NVarCharMax("@PNumber3", d.PNumber3),
        Sql.NVarCharMax("@EngineerSupervisor", d.EngineerSupervisor),
        Sql.NVarCharMax("@QaInspector", d.QaInspector),
    ];

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