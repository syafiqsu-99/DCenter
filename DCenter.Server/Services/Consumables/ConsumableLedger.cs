using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
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

public sealed record BinRow(int LotId, int ItemId, int? CompartmentId, decimal Kg, DateTime? LastInAt = null);

public sealed record LotLedgerRow(
    int LotId, int ItemId, decimal NormalKg, decimal BakingKg, decimal ActivatedKg, decimal ReceivedKg, decimal TakenKg,
    DateOnly? LastIssuedOn, DateOnly? FirstReceivedOn, string? FirstSource, string? FirstReceivedBy)
{
    public StageTotals Totals => new(NormalKg, BakingKg, ActivatedKg);
}

public sealed record MonthlyFlow(
    int ItemId, string Category, string Diameter, string Specification, int Year, int Month, string TxnType, decimal Kg);

public sealed record WelderFlow(int ItemId, decimal Picked, decimal Returned, int? LastIssueId);

public sealed record BakingFlags(bool RebakeReturned, bool Placed, decimal? IssuedToActivatedKg);

// Which ledger lines a stock total is built from: one set of consumables, one category, lines that touch any
// compartment, or lines that touch one compartment. Empty means every live line.
public sealed record LedgerFilter(
    IReadOnlyCollection<int>? ItemIds = null, string? Category = null, bool AnyCompartment = false, int? CompartmentId = null)
{
    public static readonly LedgerFilter All = new();

    public static LedgerFilter ForItem(int itemId) => new([itemId]);

    public static LedgerFilter ForItems(IReadOnlyCollection<int> itemIds) => new(itemIds);

    public static LedgerFilter ForCategory(string? category) => new(Category: category);
}

public class ConsumableLedger(StoredProcedures sp, ConsumableStore store, TimeProvider time)
{
    // Stock per lot and stage, plus received / taken totals, last issue date and first receipt.
    public Task<List<LotLedgerRow>> LotsAsync(LedgerFilter filter, CancellationToken ct)
        => sp.QueryAsync<LotLedgerRow>("SP_DCenter_Ledger_Lots", ct,
            Sql.IdList("@ItemIds", filter.ItemIds), Sql.NVarChar("@Category", filter.Category, 30));

    public async Task<List<LotStageRow>> LotStagesAsync(LedgerFilter filter, CancellationToken ct)
        => (await LotsAsync(filter, ct)).Select(r => new LotStageRow(r.LotId, r.ItemId, r.Totals)).ToList();

    public Task<List<LotStageRow>> LotStagesForItemAsync(int itemId, CancellationToken ct)
        => LotStagesAsync(LedgerFilter.ForItem(itemId), ct);

    public async Task<List<(int LotId, decimal Available)>> NormalLotsAsync(int itemId, CancellationToken ct)
        => (await LotStagesForItemAsync(itemId, ct)).Select(l => (LotId: l.LotId, Available: l.Totals.NormalKg)).ToList();

    public Task<List<BinRow>> ActivatedBinsAsync(LedgerFilter filter, CancellationToken ct)
        => sp.QueryAsync<BinRow>("SP_DCenter_Ledger_ActivatedBins", ct,
            Sql.IdList("@ItemIds", filter.ItemIds), Sql.NVarChar("@Category", filter.Category, 30),
            Sql.Bit("@AnyCompartment", filter.AnyCompartment), Sql.Int("@CompartmentId", filter.CompartmentId));

    // Kg per consumable, month and transaction type from a date (and before another, when given).
    public Task<List<MonthlyFlow>> MonthlyAsync(DateOnly from, DateOnly? before, string? category, CancellationToken ct)
        => sp.QueryAsync<MonthlyFlow>("SP_DCenter_Ledger_Monthly", ct,
            Sql.Date("@From", from), Sql.Date("@Before", before), Sql.NVarChar("@Category", category, 30));

    // What a welder picked and returned per consumable within a date window.
    public Task<List<WelderFlow>> WelderAsync(
        int welderId, DateOnly since, DateOnly? until, int? itemId, string? category, CancellationToken ct)
        => sp.QueryAsync<WelderFlow>("SP_DCenter_Ledger_Welder", ct,
            Sql.Int("@WelderId", welderId), Sql.Date("@Since", since), Sql.Date("@Until", until),
            Sql.Int("@ItemId", itemId), Sql.NVarChar("@Category", category, 30));

    public async Task<Dictionary<int, decimal>> BakingBalancesAsync(List<int> recordIds, CancellationToken ct)
        => (await BakingFactsAsync(recordIds, ct)).ToDictionary(r => r.Id, r => r.BalanceKg);

    public async Task<BakingFlags> BakingFlagsAsync(int bakingRecordId, CancellationToken ct)
        => (await BakingFactsAsync([bakingRecordId], ct)).FirstOrDefault() is { } f
            ? new BakingFlags(f.Rebake > 0, f.Placed > 0, f.IssuedToActivatedKg)
            : new BakingFlags(false, false, null);

    public async Task RefreshBakingStatusAsync(IEnumerable<int> recordIds, CancellationToken ct)
    {
        var ids = recordIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var records = await store.BakingRecordsAsync(ids, ct);
        var facts = await BakingFactsAsync(ids, ct);

        var changed = new List<BakingRecord>();
        foreach (var record in records)
        {
            var f = facts.FirstOrDefault(x => x.Id == record.Id);
            var status = DeriveStatus(record, (f?.Sent ?? 0) > 0, (f?.Rebake ?? 0) > 0, f?.BalanceKg ?? 0m);
            if (status == record.Status) continue;
            record.Status = status;
            changed.Add(record);
        }
        await store.SaveBakingAsync(changed, ct);
    }

    private Task<List<BakingFact>> BakingFactsAsync(List<int> ids, CancellationToken ct)
        => sp.QueryAsync<BakingFact>("SP_DCenter_Ledger_Baking", ct, Sql.IdList("@Ids", ids));

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

    public async Task<Dictionary<int, StageTotals>> ItemTotalsAsync(List<int> itemIds, CancellationToken ct)
    {
        var rows = await LotStagesAsync(LedgerFilter.ForItems(itemIds), ct);
        return rows.GroupBy(r => r.ItemId).ToDictionary(g => g.Key, g => StageTotals.Sum(g.Select(x => x.Totals)));
    }

    public async Task<StageBalance> StageBalanceAsync(int itemId, CancellationToken ct)
    {
        var item = (await store.ItemAsync(itemId, ct))!;
        var totals = StageTotals.Sum((await LotStagesForItemAsync(itemId, ct)).Select(r => r.Totals));
        return new StageBalance(itemId, Cat.DiaSpec(item.Diameter, item.Specification),
            totals.NormalKg, totals.BakingKg, totals.ActivatedKg, totals.TotalKg);
    }

    public async Task<Dictionary<int, string>> CompartmentLabelsAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<int, string>();
        return (await store.CompartmentsAsync(list, ct)).ToDictionary(c => c.Id, c => c.Label);
    }

    public Task<string> NextTxnNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.TxnSequence, "CT", "000000", ct);

    public Task<string> NextBakingNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.BakingSequence, "BK", "0000", ct);

    public Task<string> NextHoldingNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.HoldingSequence, "HD", "0000", ct);

    public Task<string> NextStockCountNoAsync(CancellationToken ct) => NextNumberAsync(ConsumableStockModel.StockCountSequence, "SC", "0000", ct);

    private async Task<string> NextNumberAsync(string sequence, string prefix, string pattern, CancellationToken ct)
    {
        var next = await sp.ScalarAsync<long>("SP_DCenter_Sequence_Next", ct, Sql.NVarChar("@Sequence", sequence, 50));
        return $"{prefix}-{time.LocalNow():yy}-{next.ToString(pattern)}";
    }

    private sealed record BakingFact(int Id, int Sent, int Rebake, int Placed, decimal BalanceKg, decimal IssuedToActivatedKg);

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
