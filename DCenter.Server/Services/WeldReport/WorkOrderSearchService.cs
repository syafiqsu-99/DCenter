using DCenter.Server.Models;

namespace DCenter.Server.Services;

// Work orders and BOM levels from OracleBetsyDB, read through the V_WorkOrder / V_Bom views.
public class WorkOrderSearchService(StoredProcedures sp)
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 25;

    public async Task<(List<WorkOrderSummary> Items, bool HasMore)> SearchAsync(
        string? workOrderPrefix, int skip, int take, CancellationToken ct)
    {
        var term = (workOrderPrefix ?? string.Empty).Trim();
        take = Math.Clamp(take, 1, MaxPageSize);
        skip = Math.Max(0, skip);

        var items = await SummariesAsync(term.Length >= 2 ? term : null, false, skip, take + 1, ct);

        var hasMore = items.Count > take;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return (items, hasMore);
    }

    public async Task<WorkOrderSummary?> SummaryAsync(string workOrderNumber, CancellationToken ct)
        => (await SummariesAsync(workOrderNumber, true, 0, 1, ct)).FirstOrDefault();

    public const int MaxChildLookup = 1000;

    public Task<List<BomLinkDto>> ChildrenAsync(IEnumerable<string> parentItems, CancellationToken ct)
    {
        var items = parentItems
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxChildLookup);
        return sp.QueryAsync<BomLinkDto>("SP_Bom_Children", ct, Sql.TextList("@Items", items));
    }

    private Task<List<WorkOrderSummary>> SummariesAsync(string? prefix, bool exact, int skip, int take, CancellationToken ct)
        => sp.QueryAsync<WorkOrderSummary>("SP_WorkOrder_Search", ct,
            Sql.VarChar("@Prefix", prefix, 200), Sql.Bit("@Exact", exact), Sql.Int("@Skip", skip), Sql.Int("@Take", take));
}
