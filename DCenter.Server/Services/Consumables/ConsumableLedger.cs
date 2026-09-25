using System.Linq.Expressions;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Cat = DCenter.Server.Entities.StockCatalog;

namespace DCenter.Server.Services;

public sealed record StageTotals(decimal NormalKg, decimal BakingKg, decimal ActivatedKg)
{
    public static readonly StageTotals Zero = new(0m, 0m, 0m);

    public decimal TotalKg => NormalKg + BakingKg + ActivatedKg;

    public decimal Of(string stage) => stage switch
    {
        Cat.Normal => NormalKg,
        Cat.Baking => BakingKg,
        Cat.Activated => ActivatedKg,
        _ => 0m,
    };

    public static StageTotals Sum(IEnumerable<StageTotals> values)
    {
        decimal n = 0, b = 0, a = 0;
        foreach (var v in values) { n += v.NormalKg; b += v.BakingKg; a += v.ActivatedKg; }
        return new StageTotals(n, b, a);
    }
}

public sealed record LotStageRow(int LotId, int ItemId, StageTotals Totals);

public sealed record BinRow(int LotId, int ItemId, int? CompartmentId, decimal Kg);

public class ConsumableLedger(WeldReportContext db)
{
    public IQueryable<ConsumableMovement> Live()
        => db.ConsumableMovements.AsNoTracking().Where(m => !m.IsVoided && m.TxnType != Cat.TxnVoid);

    public async Task<List<LotStageRow>> LotStagesAsync(Expression<Func<ConsumableMovement, bool>>? filter, CancellationToken ct)
    {
        var q = Live();
        if (filter is not null) q = q.Where(filter);

        var rows = await q
            .GroupBy(m => new { m.LotId, m.Lot.ItemId })
            .Select(g => new
            {
                g.Key.LotId,
                g.Key.ItemId,
                Normal = g.Sum(m => (m.ToStage == Cat.Normal ? m.QuantityKg : 0m) - (m.FromStage == Cat.Normal ? m.QuantityKg : 0m)),
                Baking = g.Sum(m => (m.ToStage == Cat.Baking ? m.QuantityKg : 0m) - (m.FromStage == Cat.Baking ? m.QuantityKg : 0m)),
                Activated = g.Sum(m => (m.ToStage == Cat.Activated ? m.QuantityKg : 0m) - (m.FromStage == Cat.Activated ? m.QuantityKg : 0m)),
            })
            .ToListAsync(ct);

        return rows.Select(r => new LotStageRow(r.LotId, r.ItemId, new StageTotals(r.Normal, r.Baking, r.Activated))).ToList();
    }

    public Task<List<LotStageRow>> LotStagesForItemAsync(int itemId, CancellationToken ct)
        => LotStagesAsync(m => m.Lot.ItemId == itemId, ct);

    public async Task<List<BinRow>> ActivatedBinsAsync(Expression<Func<ConsumableMovement, bool>>? filter, CancellationToken ct)
    {
        var q = Live();
        if (filter is not null) q = q.Where(filter);

        var ins = await q.Where(m => m.ToStage == Cat.Activated)
            .GroupBy(m => new { m.LotId, m.Lot.ItemId, Bin = m.ToCompartmentId })
            .Select(g => new { g.Key.LotId, g.Key.ItemId, g.Key.Bin, Kg = g.Sum(m => m.QuantityKg) })
            .ToListAsync(ct);
        var outs = await q.Where(m => m.FromStage == Cat.Activated)
            .GroupBy(m => new { m.LotId, m.Lot.ItemId, Bin = m.FromCompartmentId })
            .Select(g => new { g.Key.LotId, g.Key.ItemId, g.Key.Bin, Kg = g.Sum(m => m.QuantityKg) })
            .ToListAsync(ct);

        return ins.Select(i => (i.LotId, i.ItemId, i.Bin, i.Kg))
            .Concat(outs.Select(o => (o.LotId, o.ItemId, o.Bin, Kg: -o.Kg)))
            .GroupBy(x => (x.LotId, x.ItemId, x.Bin))
            .Select(g => new BinRow(g.Key.LotId, g.Key.ItemId, g.Key.Bin, g.Sum(x => x.Kg)))
            .Where(b => b.Kg != 0)
            .ToList();
    }

    public async Task<Dictionary<int, decimal>> BakingBalancesAsync(List<int>? recordIds, CancellationToken ct)
    {
        var q = Live().Where(m => m.BakingRecordId != null);
        if (recordIds is not null) q = q.Where(m => recordIds.Contains(m.BakingRecordId!.Value));
        var rows = await q
            .GroupBy(m => m.BakingRecordId)
            .Select(g => new
            {
                Id = g.Key,
                Kg = g.Sum(m => (m.ToStage == Cat.Baking ? m.QuantityKg : 0m) - (m.FromStage == Cat.Baking ? m.QuantityKg : 0m)),
            })
            .ToListAsync(ct);
        return rows.Where(r => r.Id is not null).ToDictionary(r => r.Id!.Value, r => r.Kg);
    }

    public async Task RefreshBakingStatusAsync(IEnumerable<int> recordIds, CancellationToken ct)
    {
        var ids = recordIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var records = await db.BakingRecords.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
        var facts = await Live()
            .Where(m => m.BakingRecordId != null && ids.Contains(m.BakingRecordId!.Value))
            .GroupBy(m => m.BakingRecordId)
            .Select(g => new
            {
                Id = g.Key,
                Sent = g.Sum(m => m.TxnType == Cat.TxnSendToBake ? 1 : 0),
                Rebake = g.Sum(m => m.TxnType == Cat.TxnReturn && m.ToStage == Cat.Baking ? 1 : 0),
                Balance = g.Sum(m => (m.ToStage == Cat.Baking ? m.QuantityKg : 0m) - (m.FromStage == Cat.Baking ? m.QuantityKg : 0m)),
            })
            .ToListAsync(ct);

        foreach (var record in records)
        {
            var f = facts.FirstOrDefault(x => x.Id == record.Id);
            record.Status = DeriveStatus(record, (f?.Sent ?? 0) > 0, (f?.Rebake ?? 0) > 0, f?.Balance ?? 0m);
        }
    }

    public static string DeriveStatus(BakingRecord r, bool sent, bool rebakeReturned, decimal balance)
    {
        if (!sent) return Cat.StatusCancelled;
        if (rebakeReturned)
        {
            if (r.RebakeStart is null) return Cat.StatusRebakeQueued;
            if (r.RebakeStop is null) return Cat.StatusRebaking;
            return balance > 0 ? Cat.StatusRebaked : Cat.StatusClosed;
        }
        if (r.BakeStart is null) return Cat.StatusQueued;
        if (r.BakeStop is null) return Cat.StatusBaking;
        return balance > 0 ? Cat.StatusBaked : Cat.StatusClosed;
    }

    public async Task<Dictionary<int, StageTotals>> ItemTotalsAsync(List<int>? itemIds, CancellationToken ct)
    {
        var rows = itemIds is null
            ? await LotStagesAsync(null, ct)
            : await LotStagesAsync(m => itemIds.Contains(m.Lot.ItemId), ct);
        return rows.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => StageTotals.Sum(g.Select(x => x.Totals)));
    }

    public async Task<StageBalance> StageBalanceAsync(int itemId, CancellationToken ct)
    {
        var item = await db.ConsumableItems.AsNoTracking()
            .Where(i => i.Id == itemId)
            .Select(i => new { i.Diameter, i.Specification })
            .FirstAsync(ct);
        var totals = StageTotals.Sum((await LotStagesForItemAsync(itemId, ct)).Select(r => r.Totals));
        return new StageBalance(itemId, Cat.DiaSpec(item.Diameter, item.Specification),
            totals.NormalKg, totals.BakingKg, totals.ActivatedKg, totals.TotalKg);
    }

    public async Task<Dictionary<int, string>> CompartmentLabelsAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<int, string>();
        return await db.OvenCompartments.AsNoTracking()
            .Where(c => list.Contains(c.Id))
            .Select(c => new { c.Id, Label = c.Oven.Code + "-" + c.Label })
            .ToDictionaryAsync(c => c.Id, c => c.Label, ct);
    }

    public Task<string> NextTxnNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.TxnSequence, "CT", "000000", ct);

    public Task<string> NextBakingNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.BakingSequence, "BK", "0000", ct);

    public Task<string> NextHoldingNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.HoldingSequence, "HD", "0000", ct);

    public Task<string> NextStockCountNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.StockCountSequence, "SC", "0000", ct);

    private async Task<string> NextNumberAsync(string sequence, string prefix, string pattern, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT NEXT VALUE FOR [dbo].[{sequence}]";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var next = Convert.ToInt64(await command.ExecuteScalarAsync(ct));
        return $"{prefix}-{DateTime.Now:yy}-{next.ToString(pattern)}";
    }

    public static IQueryable<TransactionDto> Project(IQueryable<ConsumableMovement> q)
        => q.Select(m => new TransactionDto(
            m.Id, m.TxnNo, m.TxnType, m.TxnDate, m.CreatedAt, m.CreatedBy,
            m.Lot.ItemId, m.Lot.Item.Category, m.Lot.Item.Specification, m.Lot.Item.Diameter.ToString(),
            m.Lot.Item.Diameter.ToString() + " " + m.Lot.Item.Specification,
            m.LotId, m.Lot.Brand, m.Lot.LotNumber, m.QuantityKg, m.FromStage, m.ToStage,
            m.Source, m.Requestor, m.WelderId, m.Welder != null ? m.Welder.WelderName : null,
            m.Reason, m.CountedQtyKg, m.ReferenceNo, m.Remarks, m.IsVoided, m.VoidsMovementId,
            m.FromCompartmentId, m.FromCompartment != null ? m.FromCompartment.Oven.Code + "-" + m.FromCompartment.Label : null,
            m.ToCompartmentId, m.ToCompartment != null ? m.ToCompartment.Oven.Code + "-" + m.ToCompartment.Label : null,
            m.BakingRecordId, m.BakingRecord != null ? m.BakingRecord.BakingNo : null));

    public static List<(int LotId, decimal Kg)>? AllocateFifo(IEnumerable<(int LotId, decimal Available)> lots, decimal quantity)
    {
        var lines = new List<(int LotId, decimal Kg)>();
        var remaining = quantity;
        foreach (var (lotId, available) in lots.OrderBy(l => l.LotId))
        {
            if (remaining <= 0) break;
            if (available <= 0) continue;
            var take = Math.Min(available, remaining);
            lines.Add((lotId, take));
            remaining -= take;
        }
        return remaining > 0 ? null : lines;
    }
}
