using System.Globalization;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public class ConsumableQueryService(
    ConsumableStore store, ConsumableLedger ledger, IOptions<ConsumableOptions> options, TimeProvider time)
{
    private readonly ConsumableOptions settings = options.Value;

    private sealed record Receipt(int LotId, DateOnly Date, string? Source, string? ReceivedBy);

    public StockCatalogDto GetCatalog()
        => new(Cat.Categories, Cat.Sources, Cat.ActiveStages, Cat.AdjustReasons, Cat.OvenTypes,
            settings.FinishThresholdKg, settings.AllowElectrodeDirectTransfer, Math.Max(settings.ReturnWindowDays, 1), CompartmentCodes,
            Math.Max(settings.WelderBackdateDays, 0));

    private static readonly CompartmentCodeDto[] CompartmentCodes = FixedOvens.Ovens
        .SelectMany(o => Enumerable.Range(1, FixedOvens.CompartmentsPerOven).Select(n => new CompartmentCodeDto($"{o.Code}-{n}", o.OvenType)))
        .ToArray();

    public async Task<List<LotOption>> GetLotsAsync(int itemId, CancellationToken ct)
        => (await store.LotsAsync(null, [itemId], null, ct))
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Select(l => new LotOption(l.Id, l.Brand, l.LotNumber))
            .ToList();

    public async Task<List<decimal>> GetRecentQuantitiesAsync(int itemId, string? type, CancellationToken ct)
    {
        var txnType = type is null ? Cat.TxnReceive : T.TxnType(type) ?? Cat.TxnReceive;
        var (recent, _) = await store.TransactionsAsync(new MovementQuery(
            TxnType: txnType, ItemId: itemId, LiveOnly: true, Sort: MovementSort.IdDescending, Take: 30), ct);
        return recent.Select(m => m.QuantityKg).Distinct().Take(4).ToList();
    }

    public async Task<List<LotBalanceDto>> GetLotBalancesAsync(int itemId, bool includeZero, CancellationToken ct)
    {
        var lots = await store.LotsAsync(null, [itemId], null, ct);
        var ledgerLots = await ledger.LotsAsync(LedgerFilter.ForItem(itemId), ct);
        var stages = ledgerLots.ToDictionary(l => l.LotId, l => l.Totals);
        var receipts = FirstReceipts(ledgerLots);
        var isElectrode = (await store.ItemAsync(itemId, ct))?.Category == Cat.ElectrodeFiller;
        List<BinRow> bins = isElectrode
            ? (await ledger.ActivatedBinsAsync(LedgerFilter.ForItem(itemId), ct)).Where(b => b.Kg > 0).ToList()
            : [];
        var labels = await ledger.CompartmentLabelsAsync(
            bins.Where(b => b.CompartmentId is not null).Select(b => b.CompartmentId!.Value), ct);

        return lots
            .Select(l =>
            {
                var t = stages.GetValueOrDefault(l.Id) ?? StageTotals.Zero;
                var r = receipts.GetValueOrDefault(l.Id);
                var locations = bins.Where(b => b.LotId == l.Id)
                    .Select(b => new LotLocationDto(b.CompartmentId,
                        b.CompartmentId is int c ? labels.GetValueOrDefault(c, c.ToString()) : Cat.UnassignedBin, b.Kg))
                    .OrderBy(x => x.Label)
                    .ToList();
                return new LotBalanceDto(l.Id, l.Brand, l.LotNumber, r?.Date, r?.Source,
                    t.NormalKg, t.BakingKg, t.ActivatedKg, t.TotalKg, locations);
            })
            .Where(l => includeZero || l.TotalKg != 0)
            .OrderBy(l => l.LotId)
            .ToList();
    }

    public async Task<ServiceResult<List<ItemBalanceDto>>> GetBalancesAsync(string? category, bool includeZero, CancellationToken ct)
    {
        if (!T.TryCategoryFilter(category, out var cat))
            return ServiceResult<List<ItemBalanceDto>>.Fail("Unknown consumable type.");
        return ServiceResult<List<ItemBalanceDto>>.Ok(await BalancesAsync(cat, includeZero, ct));
    }

    public async Task<ServiceResult<List<LotStockRow>>> GetLotStockAsync(string? category, bool includeZero, CancellationToken ct)
    {
        if (!T.TryCategoryFilter(category, out var cat))
            return ServiceResult<List<LotStockRow>>.Fail("Unknown consumable type.");

        var lots = await store.LotsAsync(null, null, cat, ct);

        var flows = (await ledger.LotsAsync(LedgerFilter.ForCategory(cat), ct)).ToDictionary(f => f.LotId);
        var receipts = FirstReceipts(flows.Values);
        var stages = flows.ToDictionary(f => f.Key, f => f.Value.Totals);

        var itemTotals = stages
            .Join(lots, s => s.Key, l => l.Id, (s, l) => new { l.ItemId, s.Value.TotalKg })
            .GroupBy(x => x.ItemId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalKg));

        var rows = lots
            .Select(l =>
            {
                var t = stages.GetValueOrDefault(l.Id) ?? StageTotals.Zero;
                var r = receipts.GetValueOrDefault(l.Id);
                var f = flows.GetValueOrDefault(l.Id);
                var itemTotal = itemTotals.GetValueOrDefault(l.ItemId);
                return new LotStockRow(l.Id, l.ItemId, l.Category, l.Brand, l.Diameter, l.Specification, l.LotNumber,
                    Cat.DiaSpec(l.Diameter, l.Specification), r?.Date, r?.Source, r?.ReceivedBy,
                    f?.ReceivedKg ?? 0m, f?.TakenKg ?? 0m, t.NormalKg, t.BakingKg, t.ActivatedKg, t.TotalKg,
                    l.IsActive && l.MinStockKg > 0 && itemTotal <= l.MinStockKg);
            })
            .Where(r => includeZero || r.BalanceKg != 0)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.LotId)
            .ToList();

        return ServiceResult<List<LotStockRow>>.Ok(rows);
    }

    public async Task<ServiceResult<List<CounterItemDto>>> GetCounterAsync(int? welderId, string? category, CancellationToken ct)
    {
        if (!T.TryCategoryFilter(category, out var cat))
            return ServiceResult<List<CounterItemDto>>.Fail("Unknown consumable type.");

        var bins = (await ledger.ActivatedBinsAsync(LedgerFilter.ForCategory(cat), ct)).Where(b => b.Kg > 0).ToList();

        var activity = new Dictionary<int, (decimal Picked, decimal Returned, int? LastLotId, int? LastCompartmentId)>();
        if (welderId is int wid)
        {
            var since = time.Today().AddDays(-(Math.Max(settings.ReturnWindowDays, 1) - 1));
            var sums = await ledger.WelderAsync(wid, since, null, null, cat, ct);

            var issueIds = sums.Where(s => s.LastIssueId is not null).Select(s => s.LastIssueId!.Value).ToList();
            var lastIssues = (await store.MovementsAsync(null, false, issueIds, ct)).ToDictionary(m => m.Id);

            foreach (var s in sums.Where(s => s.Picked > 0 || s.Returned > 0))
            {
                var last = s.LastIssueId is int issueId ? lastIssues.GetValueOrDefault(issueId) : null;
                activity[s.ItemId] = (s.Picked, s.Returned, last?.LotId, last?.FromCompartmentId);
            }
        }

        var itemIds = bins.Select(b => b.ItemId).Concat(activity.Keys).Distinct().ToList();
        var items = await store.ItemsAsync(itemIds, null, ct);
        var lotIds = bins.Select(b => b.LotId).Distinct().ToList();
        var lotMeta = (await store.LotsAsync(lotIds, null, null, ct)).ToDictionary(l => l.Id);
        var labels = await ledger.CompartmentLabelsAsync(
            bins.Where(b => b.CompartmentId is not null).Select(b => b.CompartmentId!.Value), ct);

        ActivatedLotDto Lot(int lotId, decimal kg) => new(lotId, lotMeta[lotId].Brand, lotMeta[lotId].LotNumber, kg);

        var rows = items
            .OrderBy(i => i.Category).ThenBy(i => i.Specification).ThenBy(i => T.DiameterSortKey(i.Diameter))
            .Select(i =>
            {
                var itemBins = bins.Where(b => b.ItemId == i.Id).ToList();
                var lots = itemBins.GroupBy(b => b.LotId).OrderBy(g => g.Key)
                    .Select(g => Lot(g.Key, g.Sum(b => b.Kg)))
                    .ToList();
                var binDtos = itemBins.GroupBy(b => b.CompartmentId)
                    .Select(g => new CounterBinDto(g.Key,
                        g.Key is int c ? labels.GetValueOrDefault(c, c.ToString())
                            : i.Category == Cat.ElectrodeFiller ? Cat.UnassignedBin : "Rack",
                        g.Sum(b => b.Kg),
                        g.OrderBy(b => b.LotId).Select(b => Lot(b.LotId, b.Kg)).ToList()))
                    .OrderBy(b => b.Label)
                    .ToList();
                var a = activity.GetValueOrDefault(i.Id);
                return new CounterItemDto(i.Id, i.Category, Cat.DiaSpec(i.Diameter, i.Specification),
                    lots.Sum(l => l.ActivatedKg), i.ActivatedMinKg, i.FinishThresholdKg ?? settings.FinishThresholdKg,
                    a.Picked, a.Returned, a.LastLotId, a.LastCompartmentId, lots, binDtos, i.HoldingOvenType);
            })
            .ToList();

        return ServiceResult<List<CounterItemDto>>.Ok(rows);
    }

    public async Task<List<TransactionDto>> GetWelderTodayAsync(int welderId, CancellationToken ct)
        => (await store.TransactionsAsync(new MovementQuery(WelderId: welderId, CreatedFrom: time.LocalNow().Date,
            ExcludeVoidEntries: true, Sort: MovementSort.IdDescending), ct)).Rows;

    public async Task<ServiceResult<List<TransactionDto>>> GetEnteredTodayAsync(string? type, CancellationToken ct)
    {
        var txnType = T.TxnType(type);
        if (txnType is null) return ServiceResult<List<TransactionDto>>.Fail("Unknown transaction type.");

        var (rows, _) = await store.TransactionsAsync(new MovementQuery(TxnType: txnType, CreatedFrom: time.LocalNow().Date,
            Sort: MovementSort.IdDescending, Take: 200), ct);
        return ServiceResult<List<TransactionDto>>.Ok(rows);
    }

    public async Task<ServiceResult<TransactionPage>> GetTransactionsAsync(TransactionQuery p, CancellationToken ct)
    {
        string? type = null, stage = null;
        if (p.Type is not null && (type = T.TxnType(p.Type)) is null)
            return ServiceResult<TransactionPage>.Fail("Unknown transaction type.");
        if (p.Stage is not null && (stage = T.Stage(p.Stage)) is null)
            return ServiceResult<TransactionPage>.Fail("Unknown storage stage.");
        if (!T.TryCategoryFilter(p.Category, out var cat))
            return ServiceResult<TransactionPage>.Fail("Unknown consumable type.");

        var (items, total) = await store.TransactionsAsync(new MovementQuery(
            From: p.From, To: p.To, TxnType: type, Stage: stage, Category: cat, ItemId: p.ItemId, LotId: p.LotId,
            WelderId: p.WelderId, CompartmentId: p.CompartmentId, BakingRecordId: p.BakingRecordId, Terms: T.Terms(p.Q).ToList(),
            Sort: MovementSort.Newest, Skip: Math.Max(p.Skip, 0), Take: Math.Clamp(p.Take, 1, T.MaxPageSize)), ct);

        return ServiceResult<TransactionPage>.Ok(new TransactionPage(items, total));
    }

    public async Task<ServiceResult<DashboardDto>> GetDashboardAsync(string? category, CancellationToken ct)
    {
        if (!T.TryCategoryFilter(category, out var cat))
            return ServiceResult<DashboardDto>.Fail("Unknown consumable type.");

        var today = time.Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var first = monthStart.AddMonths(-11);
        var flows = await ledger.MonthlyAsync(first, null, cat, ct);

        decimal Sum(int year, int month, string txnType, string? forCategory = null)
            => flows.Where(f => f.Year == year && f.Month == month && f.TxnType == txnType
                                && (forCategory is null || f.Category == forCategory)).Sum(f => f.Kg);

        decimal NetOut(int year, int month, string? forCategory = null)
            => Sum(year, month, Cat.TxnIssue, forCategory) - Sum(year, month, Cat.TxnReturn, forCategory)
               + Sum(year, month, Cat.TxnFinish, forCategory);

        var months = Enumerable.Range(0, 12).Select(first.AddMonths).ToList();
        var inOut = months
            .Select(m => new MonthInOut(MonthLabel(m), Sum(m.Year, m.Month, Cat.TxnReceive), NetOut(m.Year, m.Month), m))
            .ToList();
        string[] categories = cat is null ? Cat.Categories : [cat];
        var consumption = categories
            .Select(c => new CategorySeries(c, months.Select(m => NetOut(m.Year, m.Month, c)).ToList()))
            .ToList();

        var balances = await BalancesAsync(cat, false, ct);

        List<BinRow> holdingBins = cat is null or Cat.ElectrodeFiller
            ? (await ledger.ActivatedBinsAsync(new LedgerFilter(Category: Cat.ElectrodeFiller, AnyCompartment: true), ct))
                .Where(b => b.Kg > 0 && b.CompartmentId is not null)
                .ToList()
            : [];
        var totalCompartments = (await store.CompartmentsAsync(null, ct)).Count;

        var kpis = new DashboardKpis(
            balances.Sum(b => b.TotalKg), balances.Sum(b => b.NormalKg), balances.Sum(b => b.BakingKg), balances.Sum(b => b.ActivatedKg),
            holdingBins.Sum(b => b.Kg), holdingBins.Select(b => b.CompartmentId).Distinct().Count(), totalCompartments,
            Sum(today.Year, today.Month, Cat.TxnReceive), NetOut(today.Year, today.Month),
            balances.Count(b => b.IsLow), balances.Count(b => b.NeedsRefill),
            MonthLabel(monthStart), monthStart, today);

        var usage = await ItemUsageAsync(cat, monthStart, today, balances, ct);

        return ServiceResult<DashboardDto>.Ok(new DashboardDto(kpis, inOut, consumption, balances, usage));
    }

    public async Task<ServiceResult<List<ItemMonthUsageDto>>> GetMonthConsumptionAsync(
        DateOnly month, string? category, CancellationToken ct)
    {
        if (!T.TryCategoryFilter(category, out var cat))
            return ServiceResult<List<ItemMonthUsageDto>>.Fail("Unknown consumable type.");

        var start = new DateOnly(month.Year, month.Month, 1);
        var end = start.AddMonths(1);
        var rows = (await ledger.MonthlyAsync(start, end, cat, ct))
            .Where(f => f.TxnType is Cat.TxnIssue or Cat.TxnReturn or Cat.TxnFinish)
            .GroupBy(f => (f.ItemId, f.Category, f.Diameter, f.Specification))
            .Select(g => new
            {
                g.Key.ItemId, g.Key.Category, g.Key.Diameter, g.Key.Specification,
                Picked = g.Sum(f => f.TxnType == Cat.TxnIssue ? f.Kg : 0.00m),
                Returned = g.Sum(f => f.TxnType == Cat.TxnReturn ? f.Kg : 0.00m),
                Finished = g.Sum(f => f.TxnType == Cat.TxnFinish ? f.Kg : 0.00m),
            });

        return ServiceResult<List<ItemMonthUsageDto>>.Ok(rows
            .Select(r => new ItemMonthUsageDto(r.ItemId, r.Category, Cat.DiaSpec(r.Diameter, r.Specification),
                r.Picked, r.Returned, r.Finished, r.Picked - r.Returned + r.Finished))
            .OrderByDescending(r => r.NetKg).ThenBy(r => r.DiaSpec)
            .ToList());
    }

    private async Task<List<ItemUsageDto>> ItemUsageAsync(
        string? cat, DateOnly monthStart, DateOnly today, List<ItemBalanceDto> balances, CancellationToken ct)
    {
        const int averageMonths = 3;
        var from = monthStart.AddMonths(-averageMonths);
        var rows = (await ledger.MonthlyAsync(from, null, cat, ct))
            .Where(f => f.TxnType is Cat.TxnIssue or Cat.TxnReturn or Cat.TxnFinish)
            .GroupBy(f => (f.ItemId, f.Year, f.Month))
            .Select(g => new { g.Key.ItemId, g.Key.Year, g.Key.Month, Kg = g.Sum(f => f.TxnType == Cat.TxnReturn ? -f.Kg : f.Kg) })
            .ToList();

        var lastMonth = monthStart.AddMonths(-1);
        var fullMonths = Enumerable.Range(1, averageMonths).Select(i => monthStart.AddMonths(-i)).ToList();
        decimal Used(int itemId, DateOnly month)
            => rows.Where(r => r.ItemId == itemId && r.Year == month.Year && r.Month == month.Month).Sum(r => r.Kg);

        var itemIds = rows.Select(r => r.ItemId).Concat(balances.Select(b => b.ItemId)).Distinct();
        var byId = balances.ToDictionary(b => b.ItemId);
        var missing = itemIds.Where(id => !byId.ContainsKey(id)).ToList();
        var names = (await store.ItemsAsync(missing, null, ct)).ToDictionary(i => i.Id);

        return itemIds
            .Select(id =>
            {
                var b = byId.GetValueOrDefault(id);
                var n = names.GetValueOrDefault(id);
                var average = Math.Round(fullMonths.Sum(m => Used(id, m)) / averageMonths, 2);
                var onHand = b?.TotalKg ?? 0m;
                decimal? coverDays = average > 0 ? Math.Round(onHand / (average / 30m), 0) : null;
                return new ItemUsageDto(id, b?.Category ?? n!.Category, b?.DiaSpec ?? Cat.DiaSpec(n!.Diameter, n.Specification),
                    Used(id, monthStart), Used(id, lastMonth), average, onHand, b?.MinStockKg ?? n!.MinStockKg, coverDays,
                    b?.IsLow ?? false);
            })
            .Where(u => u.ThisMonthKg != 0 || u.LastMonthKg != 0 || u.AverageMonthlyKg != 0)
            .OrderByDescending(u => u.AverageMonthlyKg).ThenByDescending(u => u.ThisMonthKg)
            .ToList();
    }

    public async Task<List<NormalStockDto>> GetNormalStockAsync(string? category, CancellationToken ct)
    {
        var cat = T.TryCategoryFilter(category, out var parsed) ? parsed : Cat.ElectrodeFiller;
        return (await BalancesAsync(cat, false, ct))
            .Where(b => b.IsActive && (b.NormalKg > 0 || b.BakingKg > 0))
            .Select(b => new NormalStockDto(b.ItemId, b.Category, b.DiaSpec, b.NormalKg, b.BakingKg, b.ActivatedKg,
                b.LotCount, b.HoldingOvenType))
            .ToList();
    }

    private async Task<List<ItemBalanceDto>> BalancesAsync(string? category, bool includeZero, CancellationToken ct)
    {
        var items = await store.ItemsAsync(null, category, ct);

        var ledgerLots = await ledger.LotsAsync(LedgerFilter.ForCategory(category), ct);
        var byItem = ledgerLots
            .GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => (Totals: StageTotals.Sum(g.Select(x => x.Totals)), Lots: g.Count(x => x.Totals.TotalKg > 0)));

        var lastIssued = ledgerLots
            .Where(l => l.LastIssuedOn is not null)
            .GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => g.Max(l => l.LastIssuedOn!.Value));

        return items
            .Select(i =>
            {
                var entry = byItem.TryGetValue(i.Id, out var e) ? e : (Totals: StageTotals.Zero, Lots: 0);
                var t = entry.Totals;
                var isLow = i.IsActive && i.MinStockKg > 0 && t.TotalKg <= i.MinStockKg;
                var needsRefill = i.IsActive && i.ActivatedMinKg > 0 && t.ActivatedKg < i.ActivatedMinKg;
                return new ItemBalanceDto(i.Id, i.Category, i.Specification, i.Diameter, Cat.DiaSpec(i.Diameter, i.Specification),
                    t.NormalKg, t.BakingKg, t.ActivatedKg, t.TotalKg, i.MinStockKg, i.ActivatedMinKg,
                    i.FinishThresholdKg ?? settings.FinishThresholdKg, isLow, needsRefill, entry.Lots,
                    lastIssued.TryGetValue(i.Id, out var last) ? (DateOnly?)last : null, i.IsActive, i.HoldingOvenType);
            })
            .Where(r => includeZero || r.TotalKg != 0 || r.IsLow)
            .OrderBy(r => r.Category).ThenBy(r => r.Specification).ThenBy(r => T.DiameterSortKey(r.Diameter))
            .ToList();
    }

    private static Dictionary<int, Receipt> FirstReceipts(IEnumerable<LotLedgerRow> lots)
        => lots.Where(l => l.FirstReceivedOn is not null)
            .ToDictionary(l => l.LotId, l => new Receipt(l.LotId, l.FirstReceivedOn!.Value, l.FirstSource, l.FirstReceivedBy));

    private static string MonthLabel(DateOnly month) => month.ToString("MMM yy", CultureInfo.InvariantCulture);
}
