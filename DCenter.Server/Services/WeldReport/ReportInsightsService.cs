using System.Text.Json;
using DCenter.Server.Entities;
using DCenter.Server.Models;

namespace DCenter.Server.Services;

// Read-only views across all reports: the dashboard and the welder/WPS/heat trace.
public class ReportInsightsService(StoredProcedures sp, TimeProvider time)
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

        var json = await sp.ScalarAsync<string>("SP_Report_Dashboard", ct,
            Sql.Date("@WindowStart", windowStart), Sql.DateTime2("@WindowStartAt", windowStartAt), Sql.Int("@Take", 10));
        var data = JsonSerializer.Deserialize<DashboardData>(json, Sql.JsonOptions)!;
        var reports = data.Reports ?? [];

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

        var welderPairs = data.Welders ?? [];

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

        var topWps = data.Wps ?? [];

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
        if (!TraceFields.Contains(field)) throw new ArgumentOutOfRangeException(nameof(field), field, "Unknown trace field.");

        var rows = await sp.QueryAsync<TraceRow>("SP_Report_Trace", ct,
            Sql.NVarChar("@Field", field, 20), Sql.NVarChar("@Q", q, 4000), Sql.Int("@Take", TraceLimit + 1));

        var items = rows.Take(TraceLimit).Select(r => new TraceRowDto(
            r.WorkOrderNumber, r.PartNo, r.CompletedAt == null ? ReportStatus.Draft : ReportStatus.Completed, r.DateWelded,
            r.JointNumber, r.WpsNo, r.WelderName, r.WelderNo, r.HeatNumberLeft, r.HeatNumberRight,
            string.Join(", ", (r.HeatLots ?? "").Split('\u001f').Where(h => !string.IsNullOrWhiteSpace(h)).Distinct()))).ToList();

        return new TraceResponse(items, rows.Count > TraceLimit);
    }

    private sealed record DashboardReport(
        string WorkOrderNumber, string? PartNo, string? Description, DateTime? CompletedAt, DateTime UpdatedAt,
        DateOnly? DateWelded, int JointCount);

    // The three result sets of SP_Report_Dashboard; an empty set comes back as null.
    private sealed record DashboardData(List<DashboardReport>? Reports, List<WelderCount>? Welders, List<WpsCount>? Wps);

    private sealed record WelderCount(string? WelderNo, string? WelderName, int Count);

    private sealed record WpsCount(string WpsNo, int Count);

    // HeatLots holds the joint's electrode heat/lots in column order, separated by U+001F.
    private sealed record TraceRow(
        string WorkOrderNumber, string? PartNo, DateTime? CompletedAt, DateOnly? DateWelded, int JointNumber,
        string? WpsNo, string? WelderName, string? WelderNo, string? HeatNumberLeft, string? HeatNumberRight, string? HeatLots);
}
