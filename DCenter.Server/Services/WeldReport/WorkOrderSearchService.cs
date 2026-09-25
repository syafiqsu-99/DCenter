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
        if (await SummaryAsync(workOrderNumber, ct) is not { } wo) return [];

        var root = wo.AssemblyItem ?? string.Empty;
        List<BomTreeRow> rows = root.Length == 0
            ? []
            : await db.BomTree
                .AsNoTracking()
                .Where(t => t.RootItem == root)
                .ToListAsync(ct);

        var items = rows.Select(r => r.Component).Append(root).Where(i => i.Length > 0).Distinct().ToList();
        List<ItemMrn> mrnRows = items.Count == 0
            ? []
            : await db.ItemMrns
                .AsNoTracking()
                .Where(m => m.Item != null && items.Contains(m.Item))
                .ToListAsync(ct);

        var mrnByItem = mrnRows
            .GroupBy(m => m.Item!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(m => m.Mrn is null ? 1 : 0)
                      .ThenBy(m => m.CategorySetName, StringComparer.Ordinal)
                      .First(),
                StringComparer.OrdinalIgnoreCase);

        WorkOrderNode Node(int level, string? parent, string item, string? desc, string path)
        {
            mrnByItem.TryGetValue(item, out var mrn);
            return new WorkOrderNode
            {
                WorkOrderNumber = wo.WorkOrderNumber,
                AssemblyItem = wo.AssemblyItem,
                AssemblyDesc = wo.AssemblyDesc,
                Qty = wo.Qty,
                Level = level,
                ParentItem = parent,
                Item = item,
                ItemDesc = string.IsNullOrWhiteSpace(desc) ? mrn?.ItemDesc : desc,
                Path = path,
                Mrn = mrn?.Mrn,
                MrnDesc = mrn?.MrnDesc,
            };
        }

        var nodes = new List<WorkOrderNode> { Node(0, null, root, wo.AssemblyDesc, $"/{root}/") };
        nodes.AddRange(rows
            .OrderBy(r => r.Path, StringComparer.Ordinal)
            .Select(r => Node(r.Level, r.ParentItem, r.Component, r.ComponentDesc, r.Path)));
        return nodes;
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
