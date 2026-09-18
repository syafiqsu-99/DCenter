using DCenter.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public record WpsFilterResult(
    List<string> Mrns,
    List<string> SpecNos,
    List<string> PNos,
    List<WpsOption> Wps,
    string? Note);

public record WpsOption(string WpsNo, string? Process, string? BaseMetal, string PNo);

public class WpsFilterService(WeldReportContext db, WorkOrderSearchService workOrders)
{
    public async Task<WpsFilterResult> ForWorkOrderAsync(string workOrderNumber, CancellationToken ct)
    {
        var rows = await workOrders.PartsForWorkOrderAsync(workOrderNumber, ct);
        var mrns = rows.Select(r => r.MRN)
                       .Where(m => !string.IsNullOrWhiteSpace(m))
                       .Select(m => m!.Trim())
                       .Distinct()
                       .ToList();

        if (mrns.Count == 0)
            return new WpsFilterResult([], [], [], [], $"Work order {workOrderNumber} has no MRN, so the WPS list can't be narrowed.");

        return await ForMrnsAsync(mrns, ct);
    }

    public async Task<WpsFilterResult> ForMrnsAsync(List<string> mrns, CancellationToken ct)
    {
        var specNos = await db.MrnSpecs
            .Where(m => mrns.Contains(m.Mrn))
            .Select(m => m.SpecNo)
            .Distinct()
            .ToListAsync(ct);

        if (specNos.Count == 0)
            return new WpsFilterResult(mrns, [], [], [], "No specification is mapped to this MRN in MRN_NO.");

        var pNos = await db.BpvcMaterials
            .Where(x => specNos.Contains(x.SpecNo))
            .Select(x => x.PNo)
            .Distinct()
            .ToListAsync(ct);

        if (pNos.Count == 0)
            return new WpsFilterResult(mrns, specNos, [], [], "No P-No is mapped to this specification in BPVC_IX.");

        var wps = await db.WpsItems
            .Where(w => pNos.Contains(w.PNo))
            .OrderBy(w => w.WpsNo)
            .Select(w => new WpsOption(w.WpsNo, w.Process, w.BaseMetal, w.PNo))
            .Distinct()
            .ToListAsync(ct);

        var note = wps.Count == 0 ? "No WPS covers this P-No." : null;
        return new WpsFilterResult(mrns, specNos, pNos, wps, note);
    }
}