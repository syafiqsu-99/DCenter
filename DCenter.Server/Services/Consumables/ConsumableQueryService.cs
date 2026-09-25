using System.Globalization;
using System.Linq.Expressions;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public class ConsumableQueryService(WeldReportContext db, ConsumableLedger ledger, IOptions<ConsumableOptions> options)
{
    private const int MaxPageSize = 200;
    private readonly ConsumableOptions settings = options.Value;

    private sealed record Receipt(int LotId, DateOnly Date, string? Source, string? ReceivedBy);

    public StockCatalogDto GetCatalog()
        => new(Cat.Categories, Cat.Sources, Cat.ActiveStages, Cat.AdjustReasons, Cat.OvenTypes,
            settings.FinishThresholdKg, settings.AllowElectrodeDirectTransfer, Math.Max(settings.ReturnWindowDays, 1), CompartmentCodes);

    private static readonly CompartmentCodeDto[] CompartmentCodes = FixedOvens.Ovens
        .SelectMany(o => Enumerable.Range(1, FixedOvens.CompartmentsPerOven).Select(n => new CompartmentCodeDto($"{o.Code}-{n}", o.OvenType)))
        .ToArray();

    public Task<List<LotOption>> GetLotsAsync(int itemId, CancellationToken ct)
        => db.ConsumableItemLots.AsNoTracking()
            .Where(l => l.ItemId == itemId)
            .OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.Id)
            .Select(l => new LotOption(l.Id, l.Brand, l.LotNumber))
            .ToListAsync(ct);

    public async Task<List<decimal>> GetRecentQuantitiesAsync(int itemId, string? type, CancellationToken ct)
    {
        var txnType = type is null ? Cat.TxnReceive : T.TxnType(type) ?? Cat.TxnReceive;
        var recent = await ledger.Live()
            .Where(m => m.TxnType == txnType && m.Lot.ItemId == itemId)
            .OrderByDescending(m => m.Id)
            .Select(m => m.QuantityKg)
            .Take(30)
            .ToListAsync(ct);
        return recent.Distinct().Take(4).ToList();
    }

    public async Task<List<LotBalanceDto>> GetLotBalancesAsync(int itemId, bool includeZero, CancellationToken ct)
    {
        var lots = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => l.ItemId == itemId)
            .Select(l => new { l.Id, l.Brand, l.LotNumber })
            .ToListAsync(ct);
        var stages = (await ledger.LotStagesForItemAsync(itemId, ct)).ToDictionary(l => l.LotId, l => l.Totals);
        var receipts = await FirstReceiptsAsync(l => l.ItemId == itemId, ct);
        var isElectrode = await db.ConsumableItems.AnyAsync(i => i.Id == itemId && i.Category == Cat.ElectrodeFiller, ct);
        List<BinRow> bins = isElectrode
            ? (await ledger.ActivatedBinsAsync(m => m.Lot.ItemId == itemId, ct)).Where(b => b.Kg > 0).ToList()
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

        var lotQuery = db.ConsumableItemLots.AsNoTracking();
        if (cat is not null) lotQuery = lotQuery.Where(l => l.Item.Category == cat);
        var lots = await lotQuery
            .Select(l => new
            {
                l.Id, l.ItemId, l.Item.Category, l.Brand, l.Item.Diameter, l.Item.Specification, l.LotNumber, l.Item.MinStockKg, l.Item.IsActive,
            })
            .ToListAsync(ct);

        var receipts = await FirstReceiptsAsync(cat is null ? null : l => l.Item.Category == cat, ct);

        var flowQuery = ledger.Live();
        if (cat is not null) flowQuery = flowQuery.Where(m => m.Lot.Item.Category == cat);
        var flows = await flowQuery
            .GroupBy(m => m.LotId)
            .Select(g => new
            {
                LotId = g.Key,
                Received = g.Sum(m => m.TxnType == Cat.TxnReceive ? m.QuantityKg : 0m),
                Taken = g.Sum(m => m.TxnType == Cat.TxnIssue || m.TxnType == Cat.TxnFinish ? m.QuantityKg
                    : m.TxnType == Cat.TxnReturn ? -m.QuantityKg : 0m),
                Normal = g.Sum(m => (m.ToStage == Cat.Normal ? m.QuantityKg : 0m) - (m.FromStage == Cat.Normal ? m.QuantityKg : 0m)),
                Baking = g.Sum(m => (m.ToStage == Cat.Baking ? m.QuantityKg : 0m) - (m.FromStage == Cat.Baking ? m.QuantityKg : 0m)),
                Activated = g.Sum(m => (m.ToStage == Cat.Activated ? m.QuantityKg : 0m) - (m.FromStage == Cat.Activated ? m.QuantityKg : 0m)),
            })
            .ToDictionaryAsync(f => f.LotId, ct);
        var stages = flows.ToDictionary(f => f.Key, f => new StageTotals(f.Value.Normal, f.Value.Baking, f.Value.Activated));

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
                    f?.Received ?? 0m, f?.Taken ?? 0m, t.NormalKg, t.BakingKg, t.ActivatedKg, t.TotalKg,
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

        Expression<Func<ConsumableMovement, bool>>? filter = cat is null ? null : m => m.Lot.Item.Category == cat;
        var bins = (await ledger.ActivatedBinsAsync(filter, ct)).Where(b => b.Kg > 0).ToList();

        var activity = new Dictionary<int, (decimal Picked, decimal Returned, int? LastLotId, int? LastCompartmentId)>();
        if (welderId is int wid)
        {
            var since = T.Today.AddDays(-(Math.Max(settings.ReturnWindowDays, 1) - 1));
            var windowQuery = ledger.Live().Where(m => m.WelderId == wid && m.TxnDate >= since);
            if (filter is not null) windowQuery = windowQuery.Where(filter);
            var sums = await windowQuery
                .GroupBy(m => m.Lot.ItemId)
                .Select(g => new
                {
                    ItemId = g.Key,
                    Picked = g.Sum(m => m.TxnType == Cat.TxnIssue ? m.QuantityKg : 0m),
                    Returned = g.Sum(m => m.TxnType == Cat.TxnReturn ? m.QuantityKg : 0m),
                    LastIssueId = g.Max(m => m.TxnType == Cat.TxnIssue && m.FromStage == Cat.Activated ? (int?)m.Id : null),
                })
                .ToListAsync(ct);

            var issueIds = sums.Where(s => s.LastIssueId is not null).Select(s => s.LastIssueId!.Value).ToList();
            var lastIssues = await db.ConsumableMovements.AsNoTracking()
                .Where(m => issueIds.Contains(m.Id))
                .Select(m => new { m.Id, m.LotId, m.FromCompartmentId })
                .ToDictionaryAsync(m => m.Id, ct);

            foreach (var s in sums.Where(s => s.Picked > 0 || s.Returned > 0))
            {
                var last = s.LastIssueId is int issueId ? lastIssues.GetValueOrDefault(issueId) : null;
                activity[s.ItemId] = (s.Picked, s.Returned, last?.LotId, last?.FromCompartmentId);
            }
        }

        var itemIds = bins.Select(b => b.ItemId).Concat(activity.Keys).Distinct().ToList();
        var items = await db.ConsumableItems.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .Select(i => new { i.Id, i.Category, i.Specification, i.Diameter, i.ActivatedMinKg, i.FinishThresholdKg, i.HoldingOvenType })
            .ToListAsync(ct);
        var lotIds = bins.Select(b => b.LotId).Distinct().ToList();
        var lotMeta = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => lotIds.Contains(l.Id))
            .Select(l => new { l.Id, l.Brand, l.LotNumber })
            .ToDictionaryAsync(l => l.Id, ct);
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

    public Task<List<TransactionDto>> GetWelderTodayAsync(int welderId, CancellationToken ct)
    {
        var start = DateTime.Today;
        return ConsumableLedger.Project(db.ConsumableMovements.AsNoTracking()
                .Where(m => m.WelderId == welderId && m.CreatedAt >= start && m.TxnType != Cat.TxnVoid)
                .OrderByDescending(m => m.Id))
            .ToListAsync(ct);
    }

    public async Task<ServiceResult<List<TransactionDto>>> GetEnteredTodayAsync(string? type, CancellationToken ct)
    {
        var txnType = T.TxnType(type);
        if (txnType is null) return ServiceResult<List<TransactionDto>>.Fail("Unknown transaction type.");

        var start = DateTime.Today;
        var rows = await ConsumableLedger.Project(db.ConsumableMovements.AsNoTracking()
                .Where(m => m.TxnType == txnType && m.CreatedAt >= start)
                .OrderByDescending(m => m.Id)
                .Take(200))
            .ToListAsync(ct);
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

        var q = db.ConsumableMovements.AsNoTracking();
        if (p.From is DateOnly from) q = q.Where(m => m.TxnDate >= from);
        if (p.To is DateOnly to) q = q.Where(m => m.TxnDate <= to);
        if (type is not null) q = q.Where(m => m.TxnType == type);
        if (stage is not null) q = q.Where(m => m.FromStage == stage || m.ToStage == stage);
        if (cat is not null) q = q.Where(m => m.Lot.Item.Category == cat);
        if (p.ItemId is int itemId) q = q.Where(m => m.Lot.ItemId == itemId);
        if (p.LotId is int lotId) q = q.Where(m => m.LotId == lotId);
        if (p.WelderId is int welderId) q = q.Where(m => m.WelderId == welderId);
        if (p.CompartmentId is int compartmentId)
            q = q.Where(m => m.FromCompartmentId == compartmentId || m.ToCompartmentId == compartmentId);
        if (p.BakingRecordId is int bakingId) q = q.Where(m => m.BakingRecordId == bakingId);
        foreach (var term in T.Terms(p.Q))
        {
            q = q.Where(m => m.TxnNo.Contains(term) || m.Lot.Brand.Contains(term) || m.Lot.LotNumber.Contains(term)
                             || m.Lot.Item.Specification.Contains(term) || m.Lot.Item.Diameter.Contains(term)
                             || (m.Requestor != null && m.Requestor.Contains(term))
                             || (m.Remarks != null && m.Remarks.Contains(term))
                             || (m.CreatedBy != null && m.CreatedBy.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var items = await ConsumableLedger.Project(q
                .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
                .Skip(Math.Max(p.Skip, 0))
                .Take(Math.Clamp(p.Take, 1, MaxPageSize)))
            .ToListAsync(ct);

        return ServiceResult<TransactionPage>.Ok(new TransactionPage(items, total));
    }

    public async Task<ServiceResult<DashboardDto>> GetDashboardAsync(string? category, CancellationToken ct)
    {
        if (!T.TryCategoryFilter(category, out var cat))
            return ServiceResult<DashboardDto>.Fail("Unknown consumable type.");

        var today = T.Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var first = monthStart.AddMonths(-11);
        List<string> flowTypes = [Cat.TxnReceive, Cat.TxnIssue, Cat.TxnReturn, Cat.TxnFinish, Cat.TxnAdjust];

        var flowQuery = ledger.Live().Where(m => m.TxnDate >= first && flowTypes.Contains(m.TxnType));
        if (cat is not null) flowQuery = flowQuery.Where(m => m.Lot.Item.Category == cat);
        var flows = await flowQuery
            .GroupBy(m => new { m.TxnDate.Year, m.TxnDate.Month, m.TxnType, m.Lot.Item.Category })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.TxnType, g.Key.Category, Kg = g.Sum(m => m.QuantityKg) })
            .ToListAsync(ct);

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
            ? (await ledger.ActivatedBinsAsync(m => m.Lot.Item.Category == Cat.ElectrodeFiller
                                                    && (m.FromCompartmentId != null || m.ToCompartmentId != null), ct))
                .Where(b => b.Kg > 0 && b.CompartmentId is not null)
                .ToList()
            : [];
        var totalCompartments = await db.OvenCompartments.CountAsync(ct);

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
        var q = ledger.Live().Where(m => m.TxnDate >= start && m.TxnDate < end
            && (m.TxnType == Cat.TxnIssue || m.TxnType == Cat.TxnReturn || m.TxnType == Cat.TxnFinish));
        if (cat is not null) q = q.Where(m => m.Lot.Item.Category == cat);

        var rows = await q
            .GroupBy(m => new { m.Lot.ItemId, m.Lot.Item.Category, m.Lot.Item.Diameter, m.Lot.Item.Specification })
            .Select(g => new
            {
                g.Key.ItemId,
                g.Key.Category,
                g.Key.Diameter,
                g.Key.Specification,
                Picked = g.Sum(m => m.TxnType == Cat.TxnIssue ? m.QuantityKg : 0m),
                Returned = g.Sum(m => m.TxnType == Cat.TxnReturn ? m.QuantityKg : 0m),
                Finished = g.Sum(m => m.TxnType == Cat.TxnFinish ? m.QuantityKg : 0m),
            })
            .ToListAsync(ct);

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
        var q = ledger.Live().Where(m => m.TxnDate >= from
            && (m.TxnType == Cat.TxnIssue || m.TxnType == Cat.TxnReturn || m.TxnType == Cat.TxnFinish));
        if (cat is not null) q = q.Where(m => m.Lot.Item.Category == cat);
        var rows = await q
            .GroupBy(m => new { m.Lot.ItemId, m.TxnDate.Year, m.TxnDate.Month })
            .Select(g => new
            {
                g.Key.ItemId,
                g.Key.Year,
                g.Key.Month,
                Kg = g.Sum(m => m.TxnType == Cat.TxnReturn ? -m.QuantityKg : m.QuantityKg),
            })
            .ToListAsync(ct);

        var lastMonth = monthStart.AddMonths(-1);
        var fullMonths = Enumerable.Range(1, averageMonths).Select(i => monthStart.AddMonths(-i)).ToList();
        decimal Used(int itemId, DateOnly month)
            => rows.Where(r => r.ItemId == itemId && r.Year == month.Year && r.Month == month.Month).Sum(r => r.Kg);

        var itemIds = rows.Select(r => r.ItemId).Concat(balances.Select(b => b.ItemId)).Distinct();
        var byId = balances.ToDictionary(b => b.ItemId);
        var missing = itemIds.Where(id => !byId.ContainsKey(id)).ToList();
        var names = await db.ConsumableItems.AsNoTracking()
            .Where(i => missing.Contains(i.Id))
            .Select(i => new { i.Id, i.Category, i.Diameter, i.Specification, i.MinStockKg })
            .ToDictionaryAsync(i => i.Id, ct);

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
        var itemQuery = db.ConsumableItems.AsNoTracking();
        if (category is not null) itemQuery = itemQuery.Where(i => i.Category == category);
        var items = await itemQuery
            .Select(i => new
            {
                i.Id, i.Category, i.Specification, i.Diameter, i.MinStockKg, i.ActivatedMinKg, i.FinishThresholdKg, i.IsActive,
                i.HoldingOvenType,
            })
            .ToListAsync(ct);

        Expression<Func<ConsumableMovement, bool>>? filter = category is null ? null : m => m.Lot.Item.Category == category;
        var byItem = (await ledger.LotStagesAsync(filter, ct))
            .GroupBy(l => l.ItemId)
            .ToDictionary(g => g.Key, g => (Totals: StageTotals.Sum(g.Select(x => x.Totals)), Lots: g.Count(x => x.Totals.TotalKg > 0)));

        var issueQuery = ledger.Live().Where(m => m.TxnType == Cat.TxnIssue);
        if (filter is not null) issueQuery = issueQuery.Where(filter);
        var lastIssued = await issueQuery
            .GroupBy(m => m.Lot.ItemId)
            .Select(g => new { ItemId = g.Key, Last = g.Max(m => m.TxnDate) })
            .ToDictionaryAsync(x => x.ItemId, x => x.Last, ct);

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

    private async Task<Dictionary<int, Receipt>> FirstReceiptsAsync(
        Expression<Func<ConsumableItemLot, bool>>? lotFilter, CancellationToken ct)
    {
        var lots = db.ConsumableItemLots.AsNoTracking();
        if (lotFilter is not null) lots = lots.Where(lotFilter);
        var rows = await lots
            .Select(l => new
            {
                LotId = l.Id,
                First = l.Movements
                    .Where(m => !m.IsVoided && m.TxnType == Cat.TxnReceive)
                    .OrderBy(m => m.TxnDate).ThenBy(m => m.Id)
                    .Select(m => new { m.TxnDate, m.Source, m.Requestor })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);
        return rows
            .Where(r => r.First is not null)
            .ToDictionary(r => r.LotId, r => new Receipt(r.LotId, r.First!.TxnDate, r.First.Source, r.First.Requestor));
    }

    private static string MonthLabel(DateOnly month) => month.ToString("MMM yy", CultureInfo.InvariantCulture);
}
