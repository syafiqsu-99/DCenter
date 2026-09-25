using System.Text.Json;
using DCenter.Server.Data;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

public class WorkOrderSearchService(SourceContext db)
{
    public const int MaxPageSize = 100;
    public const int DefaultPageSize = 25;

    public async Task<(List<WorkOrderNode> Items, bool HasMore)> SearchAsync(
        string? workOrderPrefix, int skip, int take, CancellationToken ct)
    {
        var term = (workOrderPrefix ?? string.Empty).Trim();
        take = Math.Clamp(take, 1, MaxPageSize);
        skip = Math.Max(0, skip);

        var source = term.Length >= 2
            ? db.WorkOrderDetails.Where(w => w.WoNumber.StartsWith(term))
            : db.WorkOrderDetails;

        var numbers = await source
            .AsNoTracking()
            .Select(w => w.WoNumber)
            .Distinct()
            .OrderBy(n => n)
            .Skip(skip)
            .Take(take + 1)
            .ToListAsync(ct);

        var hasMore = numbers.Count > take;
        if (hasMore) numbers.RemoveAt(numbers.Count - 1);
        if (numbers.Count == 0) return ([], false);

        var json = JsonSerializer.Serialize(numbers);
        var nodes = await db.WorkOrderNodes
            .FromSql($"""
                SELECT b.*
                FROM OPENJSON({json}) WITH (WO varchar(240) '$') p
                CROSS APPLY dbo.fn_DCenter_WorkOrderBom(p.WO) b
                """)
            .AsNoTracking()
            .ToListAsync(ct);

        return (Ordered(nodes), hasMore);
    }

    public async Task<List<WorkOrderNode>> TreeForWorkOrderAsync(string workOrderNumber, CancellationToken ct)
    {
        var nodes = await db.WorkOrderNodes
            .FromSql($"SELECT * FROM dbo.fn_DCenter_WorkOrderBom({workOrderNumber})")
            .AsNoTracking()
            .ToListAsync(ct);
        return Ordered(nodes);
    }

    public async Task<List<string>> AllWorkOrderNumbersAsync(CancellationToken ct)
        => await db.WorkOrderDetails
            .AsNoTracking()
            .Select(w => w.WoNumber)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(ct);

    private static List<WorkOrderNode> Ordered(List<WorkOrderNode> nodes)
        => nodes
            .OrderBy(n => n.WorkOrderNumber, StringComparer.Ordinal)
            .ThenBy(n => n.Level == 0 ? 0 : 1)
            .ThenBy(n => n.Path, StringComparer.Ordinal)
            .ToList();
}
