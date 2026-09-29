using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

// Read-only views across all reports: the dashboard and the welder/WPS/heat trace.
public class ReportInsightsService(WeldReportContext db, TimeProvider time)
{
    public const int StaleDraftDays = 7;
    public const int TraceLimit = 500;

    public async Task<ReportDashboardDto> GetDashboardAsync(int months, CancellationToken ct)
    {
        months = Math.Clamp(months, 1, 24);
        var today = time.Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var windowStart = monthStart.AddMonths(-(months - 1));
        var monthStartAt = monthStart.ToDateTime(TimeOnly.MinValue);
        var windowStartAt = windowStart.ToDateTime(TimeOnly.MinValue);
        var staleBefore = time.LocalNow().AddDays(-StaleDraftDays);

        var reports = await db.Reports
            .AsNoTracking()
            .Where(r => r.ReportRequired &&
                        (r.CompletedAt == null || r.CompletedAt >= windowStartAt || r.DateWelded >= windowStart))
            .Select(r => new
            {
                r.WorkOrderNumber, r.PartNo, r.Description, r.CompletedAt, r.UpdatedAt, r.DateWelded,
                JointCount = r.Joints.Count,
            })
            .ToListAsync(ct);

        var drafts = reports.Where(r => r.CompletedAt is null).ToList();
        var openDrafts = drafts.Count;
        var staleDrafts = drafts.Count(r => r.UpdatedAt < staleBefore);
        var missingDate = drafts.Count(r => r.DateWelded is null);
        var completedThisMonth = reports.Count(r => r.CompletedAt >= monthStartAt);

        var monthly = Enumerable.Range(0, months)
            .Select(i => windowStart.AddMonths(i))
            .Select(m => new MonthCount(
                m.ToString("MMM yyyy"),
                reports.Count(r => r.CompletedAt is { } c && c.Year == m.Year && c.Month == m.Month),
                reports.Where(r => r.DateWelded is { } d && d.Year == m.Year && d.Month == m.Month).Sum(r => r.JointCount)))
            .ToList();

        var jointsThisMonth = reports.Where(r => r.DateWelded >= monthStart).Sum(r => r.JointCount);

        var windowJoints = db.Joints.AsNoTracking()
            .Where(j => j.Report.ReportRequired && j.Report.DateWelded >= windowStart);

        var welderPairs = await windowJoints
            .Where(j => j.WelderNo != null && j.WelderNo != "")
            .GroupBy(j => new { j.WelderNo, j.WelderName })
            .Select(g => new { g.Key.WelderNo, g.Key.WelderName, Count = g.Count() })
            .ToListAsync(ct);

        var topWelders = welderPairs
            .GroupBy(x => x.WelderNo!)
            .Select(g => new
            {
                WelderNo = g.Key,
                Name = g.OrderByDescending(x => x.Count).Select(x => x.WelderName).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)),
                Count = g.Sum(x => x.Count),
            })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        var topWps = await windowJoints
            .Where(j => j.WpsNo != null && j.WpsNo != "")
            .GroupBy(j => j.WpsNo!)
            .Select(g => new { WpsNo = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var needsAction = drafts
            .Where(r => r.UpdatedAt < staleBefore || r.DateWelded is null)
            .OrderBy(r => r.UpdatedAt)
            .Take(50)
            .ToList();

        return new ReportDashboardDto(
            new ReportKpis(openDrafts, completedThisMonth, jointsThisMonth, staleDrafts, missingDate,
                StaleDraftDays, monthStart.ToString("MMMM yyyy")),
            monthly,
            topWelders.Select(w => new NameCount(w.Name ?? w.WelderNo, w.WelderNo, w.Count)).ToList(),
            topWps.Select(w => new NameCount(w.WpsNo, null, w.Count)).ToList(),
            needsAction.Select(r => new NeedsActionDto(
                r.WorkOrderNumber, r.PartNo, r.Description, r.JointCount, r.DateWelded, r.UpdatedAt,
                r.DateWelded is null
                    ? (r.UpdatedAt < staleBefore ? "No date welded · stale" : "No date welded")
                    : $"No update for {(int)(time.LocalNow() - r.UpdatedAt).TotalDays} days")).ToList());
    }

    public static readonly string[] TraceFields = ["welder", "wps", "heat", "heatLot"];

    public async Task<TraceResponse> TraceAsync(string field, string q, CancellationToken ct)
    {
        var joints = db.Joints.AsNoTracking();
        joints = field switch
        {
            "welder" => joints.Where(j => (j.WelderNo != null && j.WelderNo.Contains(q)) ||
                                          (j.WelderName != null && j.WelderName.Contains(q))),
            "wps" => joints.Where(j => j.WpsNo != null && j.WpsNo.Contains(q)),
            "heat" => joints.Where(j => (j.HeatNumberLeft != null && j.HeatNumberLeft.Contains(q)) ||
                                        (j.HeatNumberRight != null && j.HeatNumberRight.Contains(q))),
            "heatLot" => joints.Where(j => j.Materials.Any(m => m.HeatLot != null && m.HeatLot.Contains(q))),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown trace field."),
        };

        var rows = await joints
            .OrderByDescending(j => j.Report.DateWelded)
            .ThenBy(j => j.Report.WorkOrderNumber)
            .ThenBy(j => j.JointNumber)
            .Take(TraceLimit + 1)
            .Select(j => new
            {
                j.Report.WorkOrderNumber, j.Report.PartNo, j.Report.CompletedAt, j.Report.DateWelded,
                j.JointNumber, j.WpsNo, j.WelderName, j.WelderNo, j.HeatNumberLeft, j.HeatNumberRight,
                HeatLots = j.Materials.OrderBy(m => m.ColumnNumber).Select(m => m.HeatLot).ToList(),
            })
            .ToListAsync(ct);

        var items = rows.Take(TraceLimit).Select(r => new TraceRowDto(
            r.WorkOrderNumber, r.PartNo, r.CompletedAt == null ? ReportStatus.Draft : ReportStatus.Completed, r.DateWelded,
            r.JointNumber, r.WpsNo, r.WelderName, r.WelderNo, r.HeatNumberLeft, r.HeatNumberRight,
            string.Join(", ", r.HeatLots.Where(h => !string.IsNullOrWhiteSpace(h)).Distinct()))).ToList();

        return new TraceResponse(items, rows.Count > TraceLimit);
    }
}
