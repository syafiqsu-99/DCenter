using DCenter.Server.Data;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class WorkOrderSearchService(SourceContext db)
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 50;

    private IQueryable<WorkOrderRow> BaseQuery(IQueryable<WorkOrderDetail> workOrders) =>
        from wod in workOrders

        join bom in db.BillOfMaterialOthers
            on wod.AssemblyItem equals bom.Item into bomGroup
        from bom in bomGroup.DefaultIfEmpty()

        join mrn in db.MRNCategory
            on bom != null ? bom.Item : null equals mrn.Item into mrnGroup
        from mrn in mrnGroup.DefaultIfEmpty()

        orderby wod.WoNumber

        select new WorkOrderRow
        {
            WorkOrderNumber = wod.WoNumber,
            AssemblyItem = wod.AssemblyItem,
            ItemDesc = wod.ItemDesc,
            Qty = wod.StartQuantity,
            ChildPart = bom != null ? bom.Component : null,
            ComponentDesc = bom != null ? bom.ComponentDesc : null,
            MRN = mrn != null ? mrn.MRN : null,
            MRNDesc = mrn != null ? mrn.MRNDesc : null
        };

    public async Task<(List<WorkOrderRow> Items, bool HasMore)> SearchAsync(
        string? workOrderPrefix, int skip, int take, CancellationToken ct)
    {
        var term = (workOrderPrefix ?? string.Empty).Trim();
        take = Math.Clamp(take, 1, MaxPageSize);
        skip = Math.Max(0, skip);

        var source = term.Length >= 2
            ? db.WorkOrderDetails.Where(w => w.WoNumber.StartsWith(term))
            : db.WorkOrderDetails;

        var rows = await BaseQuery(source)
            .AsNoTracking()
            .Skip(skip)
            .Take(take + 1)
            .ToListAsync(ct);

        var hasMore = rows.Count > take;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        return (rows, hasMore);
    }

    public async Task<List<WorkOrderRow>> PartsForWorkOrderAsync(string workOrderNumber, CancellationToken ct)
        => await BaseQuery(db.WorkOrderDetails.Where(w => w.WoNumber == workOrderNumber))
            .AsNoTracking().ToListAsync(ct);

    public async Task<List<string>> AllWorkOrderNumbersAsync(CancellationToken ct)
        => await db.WorkOrderDetails
            .AsNoTracking()
            .Select(w => w.WoNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);
}