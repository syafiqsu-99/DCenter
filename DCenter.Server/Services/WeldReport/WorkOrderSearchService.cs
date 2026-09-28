using DCenter.Server.Data;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class WorkOrderSearchService(ErpViewContext db)
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 25;

    public async Task<(List<WorkOrderSummary> Items, bool HasMore)> SearchAsync(
        string? workOrderPrefix, int skip, int take, CancellationToken ct)
    {
        var term = (workOrderPrefix ?? string.Empty).Trim();
        take = Math.Clamp(take, 1, MaxPageSize);
        skip = Math.Max(0, skip);

        var source = term.Length >= 2
            ? db.WorkOrderDetails.Where(w => w.WoNumber.StartsWith(term))
            : db.WorkOrderDetails;

        var items = await Summaries(source, skip, take + 1).ToListAsync(ct);

        var hasMore = items.Count > take;
        if (hasMore) items.RemoveAt(items.Count - 1);
        return (items, hasMore);
    }

    public Task<WorkOrderSummary?> SummaryAsync(string workOrderNumber, CancellationToken ct)
        => Summaries(db.WorkOrderDetails.Where(w => w.WoNumber == workOrderNumber), 0, 1)
            .FirstOrDefaultAsync(ct);

    public async Task<List<WorkOrderNode>> TreeForWorkOrderAsync(string workOrderNumber, CancellationToken ct)
    {
        var nodes = await db.Database
            .SqlQuery<WorkOrderNode>($"""
                SELECT WorkOrderNumber, AssemblyItem, AssemblyDesc, Qty, [Level], ParentItem, Item, ItemDesc, [Path], Mrn, MrnDesc
                FROM dbo.fn_DCenter_WorkOrderParts({workOrderNumber})
                """)
            .ToListAsync(ct);

        return nodes
            .OrderBy(n => n.Level == 0 ? 0 : 1)
            .ThenBy(n => n.Path, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<List<string>> AllWorkOrderNumbersAsync(CancellationToken ct)
        => await db.WorkOrderDetails
            .AsNoTracking()
            .Select(w => w.WoNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);

    private static IQueryable<WorkOrderSummary> Summaries(IQueryable<WorkOrderDetail> source, int skip, int take)
        => source
            .AsNoTracking()
            .GroupBy(w => w.WoNumber)
            .OrderBy(g => g.Key)
            .Skip(skip)
            .Take(take)
            .Select(g => new WorkOrderSummary(
                g.Key,
                g.Max(w => w.AssemblyItem),
                g.Max(w => w.ItemDesc),
                g.Max(w => w.StartQuantity)));
}
