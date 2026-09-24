using System.Linq.Expressions;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public class StockCountService(WeldReportContext db, ConsumableLedger ledger)
{
    private const int MaxLines = 2000;

    public async Task<ServiceResult<CountSheetDto>> GetSheetAsync(string? scope, string? category, CancellationToken ct)
    {
        var stage = Scope(scope);
        if (stage is null) return ServiceResult<CountSheetDto>.Fail("Scope must be Activated or Normal.");
        if (!T.TryCategoryFilter(category, out var cat)) return ServiceResult<CountSheetDto>.Fail("Unknown consumable type.");

        Expression<Func<ConsumableMovement, bool>>? filter = cat is null ? null : m => m.Lot.Item.Category == cat;
        var raw = stage == Cat.Normal
            ? (await ledger.LotStagesAsync(filter, ct))
                .Where(l => l.Totals.NormalKg > 0)
                .Select(l => (l.LotId, CompartmentId: (int?)null, Kg: l.Totals.NormalKg))
                .ToList()
            : (await ledger.ActivatedBinsAsync(filter, ct))
                .Where(b => b.Kg > 0)
                .Select(b => (b.LotId, b.CompartmentId, b.Kg))
                .ToList();

        var lotIds = raw.Select(r => r.LotId).Distinct().ToList();
        var meta = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .Select(l => new { l.Id, l.ItemId, l.Brand, l.LotNumber, l.Item.Category, l.Item.Specification, l.Item.Diameter })
            .ToDictionaryAsync(l => l.Id, ct);
        var labels = await ledger.CompartmentLabelsAsync(
            raw.Where(r => r.CompartmentId is not null).Select(r => r.CompartmentId!.Value), ct);

        var lines = raw
            .Select(r =>
            {
                var m = meta[r.LotId];
                var location = stage == Cat.Normal ? "Main store"
                    : r.CompartmentId is int c ? labels.GetValueOrDefault(c, c.ToString())
                    : m.Category == Cat.ElectrodeFiller ? Cat.UnassignedBin : "Rack";
                return new CountLineDto($"{r.LotId}:{r.CompartmentId?.ToString() ?? "-"}", m.ItemId, m.Category, m.Specification,
                    m.Diameter, Cat.DiaSpec(m.Diameter, m.Specification), r.LotId, m.Brand, m.LotNumber, stage, r.CompartmentId,
                    location, r.Kg);
            })
            .OrderBy(l => l.Category)
            .ThenBy(l => l.CompartmentId is null ? 0 : 1)
            .ThenBy(l => l.Location)
            .ThenBy(l => l.Specification)
            .ThenBy(l => T.DiameterSortKey(l.Diameter))
            .ThenBy(l => l.LotId)
            .ToList();

        return ServiceResult<CountSheetDto>.Ok(new CountSheetDto(stage, cat, DateTime.Now, lines));
    }

    public async Task<ServiceResult<StockCountDto>> PostAsync(StockCountRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.CountDate);
        if (error is not null) return Fail(error);

        var stage = Scope(r.Scope);
        if (stage is null) return Fail("Scope must be Activated or Normal.");
        if (!T.TryCategoryFilter(r.Category, out var cat)) return Fail("Unknown consumable type.");

        var input = r.Lines ?? new List<CountLineInput>();
        if (input.Count == 0) return Fail("The count has no lines.");
        if (input.Count > MaxLines) return Fail($"A count can have at most {MaxLines} lines.");
        if (input.Any(l => l.CountedKg < 0)) return Fail("Counted quantities cannot be negative.");
        if (input.GroupBy(l => (l.LotId, l.CompartmentId)).Any(g => g.Count() > 1))
            return Fail("The same lot and location appears more than once in the count.");
        if (stage == Cat.Normal && input.Any(l => l.CompartmentId is not null))
            return Fail("Normal storage has no compartments.");

        var remarks = T.FreeText(r.Remarks, 500);
        var lines = input
            .Select(l => new { l.ItemId, l.LotId, l.CompartmentId, System = T.RoundKg(l.SystemKg), Counted = T.RoundKg(l.CountedKg) })
            .ToList();
        var changed = lines.Where(l => l.Counted != l.System).ToList();
        var itemIds = lines.Select(l => l.ItemId).Distinct().Order().ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var itemId in itemIds) await StockLocks.AcquireAsync(db, StockLocks.Item(itemId), ct);

        var lotIds = lines.Select(l => l.LotId).Distinct().ToList();
        var lotItems = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .Select(l => new { l.Id, l.ItemId, l.LotNumber, l.Item.Category })
            .ToDictionaryAsync(l => l.Id, ct);
        foreach (var line in lines)
        {
            if (!lotItems.TryGetValue(line.LotId, out var lot) || lot.ItemId != line.ItemId)
                return Fail("A count line refers to a lot that does not belong to its consumable. Reload the count sheet.");
            if (line.CompartmentId is not null && lot.Category != Cat.ElectrodeFiller)
                return Fail("Bare & powder fillers are not stored in oven compartments.");
        }

        Dictionary<(int LotId, int? Bin), decimal> current;
        if (stage == Cat.Normal)
        {
            current = (await ledger.LotStagesAsync(m => itemIds.Contains(m.Lot.ItemId), ct))
                .ToDictionary(l => (l.LotId, (int?)null), l => l.Totals.NormalKg);
        }
        else
        {
            current = (await ledger.ActivatedBinsAsync(m => itemIds.Contains(m.Lot.ItemId), ct))
                .GroupBy(b => (b.LotId, b.CompartmentId))
                .ToDictionary(g => g.Key, g => g.Sum(b => b.Kg));
        }

        var moved = changed
            .Where(l => current.GetValueOrDefault((l.LotId, l.CompartmentId)) != l.System)
            .Select(l => lotItems[l.LotId].LotNumber)
            .Distinct()
            .ToList();
        if (moved.Count > 0)
            return Fail($"Stock moved since the count sheet was loaded (lot {string.Join(", ", moved.Take(10))}). " +
                        "Reload the sheet and recount those lines.", StatusCodes.Status409Conflict);

        var referenceNo = await ledger.NextStockCountNoAsync(ct);
        string? txnNo = changed.Count > 0 ? await ledger.NextTxnNoAsync(ct) : null;
        decimal gain = 0m, loss = 0m;
        foreach (var line in changed)
        {
            var difference = line.Counted - line.System;
            var bin = stage == Cat.Activated ? line.CompartmentId : null;
            if (difference > 0) gain += difference;
            else loss -= difference;
            db.ConsumableMovements.Add(new ConsumableMovement
            {
                TxnNo = txnNo!,
                TxnType = Cat.TxnAdjust,
                TxnDate = date,
                LotId = line.LotId,
                QuantityKg = Math.Abs(difference),
                FromStage = difference < 0 ? stage : null,
                ToStage = difference > 0 ? stage : null,
                FromCompartmentId = difference < 0 ? bin : null,
                ToCompartmentId = difference > 0 ? bin : null,
                Reason = Cat.ReasonCountVariance,
                CountedQtyKg = line.Counted,
                ReferenceNo = referenceNo,
                Remarks = remarks,
                CreatedBy = user,
            });
        }

        var count = new StockCount
        {
            ReferenceNo = referenceNo,
            CountDate = date,
            Scope = stage,
            Category = cat,
            LinesCounted = lines.Count,
            LinesAdjusted = changed.Count,
            GainKg = gain,
            LossKg = loss,
            TxnNo = txnNo,
            Remarks = remarks,
            CreatedBy = user,
        };
        db.StockCounts.Add(count);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<StockCountDto>.Ok((await ToDtosAsync(db.StockCounts.AsNoTracking().Where(c => c.Id == count.Id), ct))[0]);
    }

    public async Task<StockCountPage> GetCountsAsync(DateOnly? from, DateOnly? to, string? scope, int skip, int take, CancellationToken ct)
    {
        var q = db.StockCounts.AsNoTracking();
        if (from is DateOnly f) q = q.Where(c => c.CountDate >= f);
        if (to is DateOnly t) q = q.Where(c => c.CountDate <= t);
        if (Scope(scope) is string stage) q = q.Where(c => c.Scope == stage);
        var total = await q.CountAsync(ct);
        var rows = await ToDtosAsync(q.OrderByDescending(c => c.Id).Skip(Math.Max(skip, 0)).Take(Math.Clamp(take, 1, 200)), ct);
        return new StockCountPage(rows, total);
    }

    public async Task<ServiceResult<StockCountDetailDto>> GetCountAsync(string referenceNo, CancellationToken ct)
    {
        var rows = await ToDtosAsync(db.StockCounts.AsNoTracking().Where(c => c.ReferenceNo == referenceNo), ct);
        if (rows.Count == 0) return ServiceResult<StockCountDetailDto>.Fail("Stock count not found.", StatusCodes.Status404NotFound);
        var count = rows[0];
        List<TransactionDto> adjustments = count.TxnNo is null
            ? []
            : await ConsumableLedger.Project(db.ConsumableMovements.AsNoTracking()
                    .Where(m => m.TxnNo == count.TxnNo)
                    .OrderBy(m => m.Lot.Item.Category).ThenBy(m => m.Lot.Item.Specification).ThenBy(m => m.Lot.LotNumber))
                .ToListAsync(ct);
        return ServiceResult<StockCountDetailDto>.Ok(new StockCountDetailDto(count, adjustments));
    }

    private async Task<List<StockCountDto>> ToDtosAsync(IQueryable<StockCount> q, CancellationToken ct)
    {
        var rows = await q.ToListAsync(ct);
        var txnNos = rows.Where(c => c.TxnNo is not null).Select(c => c.TxnNo!).ToList();
        var voided = await db.ConsumableMovements.AsNoTracking()
            .Where(m => txnNos.Contains(m.TxnNo) && m.IsVoided)
            .Select(m => m.TxnNo)
            .Distinct()
            .ToListAsync(ct);
        return rows
            .Select(c => new StockCountDto(c.Id, c.ReferenceNo, c.CountDate, c.Scope, c.Category, c.LinesCounted, c.LinesAdjusted,
                c.GainKg, c.LossKg, c.GainKg - c.LossKg, c.TxnNo, c.TxnNo is not null && voided.Contains(c.TxnNo), c.Remarks,
                c.CreatedBy, c.CreatedAt))
            .ToList();
    }

    private static string? Scope(string? raw)
        => Cat.ActiveStages.FirstOrDefault(s => string.Equals(s, raw?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static ServiceResult<StockCountDto> Fail(string error, int status = StatusCodes.Status400BadRequest)
        => ServiceResult<StockCountDto>.Fail(error, status);
}
