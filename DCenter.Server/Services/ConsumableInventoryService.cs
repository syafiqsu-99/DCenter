using System.Data;
using System.Globalization;
using System.Text;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Cat = DCenter.Server.Entities.ConsumableCatalog;

namespace DCenter.Server.Services;

public class ConsumableInventoryService(WeldReportContext db)
{
    private const string MasterLock = "DCenter.Consumables.Master";
    private const int MaxPageSize = 200;
    private const decimal DefaultMinStockKg = 10m;

    private static readonly string[] DateFormats = ["d/M/yyyy", "dd/MM/yyyy", "d/M/yy", "yyyy-MM-dd", "d-M-yyyy", "d.M.yyyy"];

    private static readonly Dictionary<string, string> RequiredColumns = new()
    {
        ["source"] = "Source",
        ["consumabletype"] = "Consumable Type",
        ["electrodebrand"] = "Electrode Brand",
        ["electrodediametermm"] = "Electrode Diameter (mm)",
        ["electrodespecification"] = "Electrode Specification",
        ["lotheatnumber"] = "Lot\\ heat Number",
        ["receiveqtykg"] = "Receive Qty (KG)",
    };

    private sealed record StockAgg(
        int LotId, string Location, int ConsumableId, string LotNumber, string ConsumableType,
        string Manufacturer, string Specification, string Diameter, decimal MinStockKg,
        decimal ReceivedKg, decimal IssuedKg, decimal BalanceKg, DateOnly FirstDate, DateTime LastMovementAt);

    private sealed record NormalizedConsumable(
        string Type, string Manufacturer, string Specification, string Diameter, decimal MinStockKg, bool IsActive);

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public ConsumableCatalogDto GetCatalog() => new(Cat.Types, Cat.Locations);

    public async Task<List<ConsumableDto>> SearchConsumablesAsync(string? q, bool activeOnly, int take, CancellationToken ct)
    {
        var query = db.Consumables.AsNoTracking();
        if (activeOnly) query = query.Where(c => c.IsActive);
        foreach (var term in Terms(q))
            query = query.Where(c => c.Manufacturer.Contains(term) || c.Specification.Contains(term)
                                     || c.Diameter.Contains(term) || c.ConsumableType.Contains(term));

        var items = await query
            .OrderBy(c => c.ConsumableType).ThenBy(c => c.Specification).ThenBy(c => c.Diameter).ThenBy(c => c.Manufacturer)
            .Take(Math.Clamp(take, 1, 2000))
            .Select(c => new { c.Id, c.ConsumableType, c.Manufacturer, c.Specification, c.Diameter, c.MinStockKg, c.IsActive })
            .ToListAsync(ct);

        var balances = await ConsumableBalancesAsync(items.Select(i => i.Id).ToList(), ct);
        return items.Select(c => new ConsumableDto(
                c.Id, c.ConsumableType, c.Manufacturer, c.Specification, c.Diameter,
                Cat.DiaSpec(c.Diameter, c.Specification), c.MinStockKg, c.IsActive, balances.GetValueOrDefault(c.Id)))
            .ToList();
    }

    public async Task<ServiceResult<ConsumableDto>> UpsertConsumableAsync(int? id, ConsumableUpsert dto, CancellationToken ct)
    {
        var (n, error) = Normalize(dto);
        if (n is null) return ServiceResult<ConsumableDto>.Fail(error!);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireLockAsync(MasterLock, ct);

        var currentId = id ?? 0;
        var duplicate = await db.Consumables.AnyAsync(c => c.Id != currentId
            && c.ConsumableType == n.Type && c.Manufacturer == n.Manufacturer
            && c.Specification == n.Specification && c.Diameter == n.Diameter, ct);
        if (duplicate)
            return ServiceResult<ConsumableDto>.Fail("This consumable already exists.", StatusCodes.Status409Conflict);

        Consumable? entity;
        if (id is int existingId)
        {
            entity = await db.Consumables.FirstOrDefaultAsync(c => c.Id == existingId, ct);
            if (entity is null) return ServiceResult<ConsumableDto>.Fail("Consumable not found.", StatusCodes.Status404NotFound);
        }
        else
        {
            entity = new Consumable();
            db.Consumables.Add(entity);
        }

        entity.ConsumableType = n.Type;
        entity.Manufacturer = n.Manufacturer;
        entity.Specification = n.Specification;
        entity.Diameter = n.Diameter;
        entity.MinStockKg = n.MinStockKg;
        entity.IsActive = n.IsActive;
        await EnsureLookupsAsync(n, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var balance = (await ConsumableBalancesAsync([entity.Id], ct)).GetValueOrDefault(entity.Id);
        return ServiceResult<ConsumableDto>.Ok(new ConsumableDto(
            entity.Id, entity.ConsumableType, entity.Manufacturer, entity.Specification, entity.Diameter,
            Cat.DiaSpec(entity.Diameter, entity.Specification), entity.MinStockKg, entity.IsActive, balance));
    }

    public async Task<List<LotOption>> GetLotsAsync(int consumableId, CancellationToken ct)
        => await db.ConsumableLots.AsNoTracking()
            .Where(l => l.ConsumableId == consumableId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LotOption(l.Id, l.LotNumber))
            .ToListAsync(ct);

    public async Task<List<decimal>> GetRecentQuantitiesAsync(int consumableId, CancellationToken ct)
    {
        var recent = await Live()
            .Where(t => t.TxnType == Cat.TxnReceive && t.Lot.ConsumableId == consumableId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.QuantityKg)
            .Take(20)
            .ToListAsync(ct);
        return recent.Distinct().Take(3).ToList();
    }

    public async Task<ServiceResult<List<StockLotOption>>> GetStockAsync(string? location, CancellationToken ct)
    {
        var loc = Cat.NormalizeLocation(location);
        if (loc is null) return ServiceResult<List<StockLotOption>>.Fail("Source must be Weldshop or Tool Crib.");

        var aggs = await Aggregates(loc).ToListAsync(ct);
        return ServiceResult<List<StockLotOption>>.Ok(aggs
            .Where(a => a.BalanceKg > 0)
            .OrderBy(a => a.FirstDate).ThenBy(a => a.LotId)
            .Select(a => new StockLotOption(
                a.LotId, a.ConsumableId, a.ConsumableType, a.Manufacturer, a.Specification, a.Diameter,
                Cat.DiaSpec(a.Diameter, a.Specification), a.LotNumber, a.Location, a.BalanceKg, a.FirstDate))
            .ToList());
    }

    public async Task<ServiceResult<List<StockCardRow>>> GetStockCardAsync(string? location, bool includeZero, CancellationToken ct)
    {
        if (!Cat.TryLocation(location, out var loc))
            return ServiceResult<List<StockCardRow>>.Fail("Source must be Weldshop, Tool Crib or All.");

        var aggs = await Aggregates(loc).ToListAsync(ct);
        if (!includeZero) aggs = aggs.Where(a => a.BalanceKg != 0).ToList();

        var totals = await ConsumableBalancesAsync(null, ct);
        var lotIds = aggs.Select(a => a.LotId).Distinct().ToList();

        var detailQuery = Live().Where(t => lotIds.Contains(t.LotId)
                                            && (t.TxnType == Cat.TxnReceive || t.TxnType == Cat.TxnIssue));
        if (loc is not null) detailQuery = detailQuery.Where(t => t.Location == loc);
        var details = await detailQuery
            .Select(t => new { t.Id, t.LotId, t.Location, t.TxnType, t.TxnDate, t.QuantityKg, t.Requestor, t.CreatedAt })
            .ToListAsync(ct);
        var byKey = details.ToLookup(d => (d.LotId, d.Location));

        var rows = aggs.Select(a =>
        {
            var movements = byKey[(a.LotId, a.Location)].ToList();
            var firstReceive = movements.Where(m => m.TxnType == Cat.TxnReceive)
                .OrderBy(m => m.TxnDate).ThenBy(m => m.CreatedAt).FirstOrDefault();
            var takes = movements.Where(m => m.TxnType == Cat.TxnIssue)
                .OrderBy(m => m.TxnDate).ThenBy(m => m.CreatedAt)
                .Select(m => new TakeDto(m.Id, m.TxnDate, -m.QuantityKg, m.Requestor))
                .ToList();

            return new StockCardRow(
                a.LotId, a.ConsumableId, firstReceive?.Requestor, firstReceive?.TxnDate ?? a.FirstDate, a.Location,
                a.ConsumableType, a.Manufacturer, a.Diameter, a.Specification, a.LotNumber,
                Cat.DiaSpec(a.Diameter, a.Specification), a.ReceivedKg, takes, a.IssuedKg, a.BalanceKg,
                a.MinStockKg, IsLow(a.MinStockKg, totals.GetValueOrDefault(a.ConsumableId)), a.LastMovementAt);
        });

        return ServiceResult<List<StockCardRow>>.Ok(rows
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.LotId).ThenBy(r => r.Source)
            .ToList());
    }

    public async Task<ServiceResult<MovementResult>> ReceiveAsync(ReceiveRequest r, CancellationToken ct)
    {
        var location = Cat.NormalizeLocation(r.Location);
        if (location is null) return ServiceResult<MovementResult>.Fail("Source must be Weldshop or Tool Crib.");

        var date = r.TxnDate ?? Today;
        if (date > Today) return ServiceResult<MovementResult>.Fail("Date cannot be in the future.");

        var qty = RoundKg(r.QuantityKg);
        if (qty <= 0) return ServiceResult<MovementResult>.Fail("Receive Qty (KG) must be greater than 0.");

        var lotNo = Trimmed(r.LotNumber);
        if (lotNo is null) return ServiceResult<MovementResult>.Fail("Lot\\ heat Number is required.");
        if (lotNo.Length > 60) return ServiceResult<MovementResult>.Fail("Lot\\ heat Number is limited to 60 characters.");

        NormalizedConsumable? newItem = null;
        if (r.ConsumableId is null)
        {
            if (r.NewConsumable is null) return ServiceResult<MovementResult>.Fail("Select a consumable or enter a new one.");
            var (n, error) = Normalize(r.NewConsumable);
            if (n is null) return ServiceResult<MovementResult>.Fail(error!);
            newItem = n;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireLockAsync(MasterLock, ct);

        Consumable? consumable;
        if (r.ConsumableId is int consumableId)
        {
            consumable = await db.Consumables.FirstOrDefaultAsync(c => c.Id == consumableId, ct);
            if (consumable is null) return ServiceResult<MovementResult>.Fail("Consumable not found.", StatusCodes.Status404NotFound);
        }
        else
        {
            consumable = await FindOrCreateConsumableAsync(newItem!, ct);
            await EnsureLookupsAsync(newItem!, ct);
        }

        var lot = await FindOrCreateLotAsync(consumable, lotNo, ct);
        var txn = new ConsumableTransaction
        {
            Lot = lot,
            TxnType = Cat.TxnReceive,
            TxnDate = date,
            Location = location,
            QuantityKg = qty,
            Requestor = FreeText(r.Requestor, 200),
            Remarks = FreeText(r.Remarks, 500),
        };
        db.ConsumableTransactions.Add(txn);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<MovementResult>.Ok(await MovementAsync(txn.Id, lot.Id, location, ct));
    }

    public async Task<ServiceResult<MovementResult>> IssueAsync(IssueRequest r, CancellationToken ct)
    {
        var location = Cat.NormalizeLocation(r.Location);
        if (location is null) return ServiceResult<MovementResult>.Fail("Source must be Weldshop or Tool Crib.");

        var date = r.TxnDate ?? Today;
        if (date > Today) return ServiceResult<MovementResult>.Fail("Date cannot be in the future.");

        var requestor = FreeText(r.Requestor, 200);
        if (requestor is null) return ServiceResult<MovementResult>.Fail("Requestor is required.");

        var qty = RoundKg(r.QuantityKg);
        if (qty <= 0) return ServiceResult<MovementResult>.Fail("Take/KG must be greater than 0.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireLockAsync(LotLock(r.LotId, location), ct);

        if (!await db.ConsumableLots.AnyAsync(l => l.Id == r.LotId, ct))
            return ServiceResult<MovementResult>.Fail("Lot not found.", StatusCodes.Status404NotFound);

        var available = await BalanceAsync(r.LotId, location, ct);
        if (qty > available)
            return ServiceResult<MovementResult>.Fail(
                $"Only {available:0.00} kg is available for this lot at {location}.", StatusCodes.Status409Conflict);

        var txn = new ConsumableTransaction
        {
            LotId = r.LotId,
            TxnType = Cat.TxnIssue,
            TxnDate = date,
            Location = location,
            QuantityKg = -qty,
            Requestor = requestor,
            Remarks = FreeText(r.Remarks, 500),
        };
        db.ConsumableTransactions.Add(txn);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<MovementResult>.Ok(await MovementAsync(txn.Id, r.LotId, location, ct));
    }

    public async Task<ServiceResult<MovementResult>> VoidAsync(int id, VoidRequest r, CancellationToken ct)
    {
        var remarks = FreeText(r.Remarks, 500);
        if (remarks is null) return ServiceResult<MovementResult>.Fail("Remarks are required to void a transaction.");

        var head = await db.ConsumableTransactions.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new { t.LotId, t.Location })
            .FirstOrDefaultAsync(ct);
        if (head is null) return ServiceResult<MovementResult>.Fail("Transaction not found.", StatusCodes.Status404NotFound);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireLockAsync(LotLock(head.LotId, head.Location), ct);

        var original = await db.ConsumableTransactions.FirstAsync(t => t.Id == id, ct);
        if (original.TxnType == Cat.TxnVoid)
            return ServiceResult<MovementResult>.Fail("A void entry cannot itself be voided.");
        if (original.IsVoided)
            return ServiceResult<MovementResult>.Fail("This transaction has already been voided.", StatusCodes.Status409Conflict);

        if (original.QuantityKg > 0)
        {
            var remaining = await BalanceAsync(original.LotId, original.Location, ct) - original.QuantityKg;
            if (remaining < 0)
                return ServiceResult<MovementResult>.Fail(
                    $"Voiding this would leave {remaining:0.00} kg for the lot at {original.Location}. Void the related stock out entries first.",
                    StatusCodes.Status409Conflict);
        }

        original.IsVoided = true;
        var voidRow = new ConsumableTransaction
        {
            LotId = original.LotId,
            TxnType = Cat.TxnVoid,
            TxnDate = original.TxnDate,
            Location = original.Location,
            QuantityKg = -original.QuantityKg,
            Requestor = original.Requestor,
            ReferenceNo = original.ReferenceNo,
            Remarks = remarks,
            VoidsTxnId = original.Id,
        };
        db.ConsumableTransactions.Add(voidRow);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<MovementResult>.Ok(await MovementAsync(voidRow.Id, original.LotId, original.Location, ct));
    }

    public async Task<ServiceResult<TransactionPage>> GetTransactionsAsync(TransactionQuery p, CancellationToken ct)
    {
        if (!Cat.TryLocation(p.Location, out var loc))
            return ServiceResult<TransactionPage>.Fail("Source must be Weldshop, Tool Crib or All.");

        var q = db.ConsumableTransactions.AsNoTracking();
        if (p.From is DateOnly from) q = q.Where(t => t.TxnDate >= from);
        if (p.To is DateOnly to) q = q.Where(t => t.TxnDate <= to);
        if (loc is not null) q = q.Where(t => t.Location == loc);
        if (p.LotId is int lotId) q = q.Where(t => t.LotId == lotId);

        var type = Cat.TxnTypes.FirstOrDefault(x => string.Equals(x, p.Type, StringComparison.OrdinalIgnoreCase));
        if (type is not null) q = q.Where(t => t.TxnType == type);

        foreach (var term in Terms(p.Q))
            q = q.Where(t => (t.Requestor != null && t.Requestor.Contains(term))
                             || t.Lot.LotNumber.Contains(term)
                             || t.Lot.Consumable.Specification.Contains(term)
                             || t.Lot.Consumable.Manufacturer.Contains(term)
                             || t.Lot.Consumable.Diameter.Contains(term)
                             || (t.ReferenceNo != null && t.ReferenceNo.Contains(term))
                             || (t.Remarks != null && t.Remarks.Contains(term)));

        var total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(t => t.TxnDate).ThenByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id))
            .Skip(Math.Max(0, p.Skip))
            .Take(Math.Clamp(p.Take, 1, MaxPageSize))
            .ToListAsync(ct);

        return ServiceResult<TransactionPage>.Ok(new TransactionPage(items, total));
    }

    public async Task<ServiceResult<List<TransactionDto>>> GetEnteredTodayAsync(string? type, CancellationToken ct)
    {
        var txnType = new[] { Cat.TxnReceive, Cat.TxnIssue }
            .FirstOrDefault(x => string.Equals(x, type, StringComparison.OrdinalIgnoreCase));
        if (txnType is null) return ServiceResult<List<TransactionDto>>.Fail("Type must be Receive or Issue.");

        var start = DateTime.Today;
        var items = await Project(db.ConsumableTransactions.AsNoTracking()
                .Where(t => t.CreatedAt >= start && t.TxnType == txnType)
                .OrderByDescending(t => t.CreatedAt))
            .Take(50)
            .ToListAsync(ct);
        return ServiceResult<List<TransactionDto>>.Ok(items);
    }

    public async Task<ServiceResult<DashboardDto>> GetDashboardAsync(string? location, CancellationToken ct)
    {
        if (!Cat.TryLocation(location, out var loc))
            return ServiceResult<DashboardDto>.Fail("Source must be Weldshop, Tool Crib or All.");

        var today = Today;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var rangeStart = monthStart.AddMonths(-11);
        var months = Enumerable.Range(0, 12).Select(i => rangeStart.AddMonths(i)).ToList();

        var aggs = await Aggregates(loc).ToListAsync(ct);

        var movementQuery = Live().Where(t => t.TxnDate >= rangeStart
                                              && (t.TxnType == Cat.TxnReceive || t.TxnType == Cat.TxnIssue));
        if (loc is not null) movementQuery = movementQuery.Where(t => t.Location == loc);
        var movements = await movementQuery
            .Select(t => new { t.TxnDate, t.TxnType, t.QuantityKg, t.Lot.Consumable.ConsumableType })
            .ToListAsync(ct);

        var inOut = months.Select(m => new MonthInOut(
                MonthLabel(m),
                movements.Where(x => x.TxnType == Cat.TxnReceive && SameMonth(x.TxnDate, m)).Sum(x => x.QuantityKg),
                -movements.Where(x => x.TxnType == Cat.TxnIssue && SameMonth(x.TxnDate, m)).Sum(x => x.QuantityKg)))
            .ToList();

        var consumption = Cat.Types.Select(type => new TypeSeries(
                type,
                months.Select(m => -movements
                        .Where(x => x.TxnType == Cat.TxnIssue && x.ConsumableType == type && SameMonth(x.TxnDate, m))
                        .Sum(x => x.QuantityKg))
                    .ToList()))
            .ToList();

        var lastIssued = await Live()
            .Where(t => t.TxnType == Cat.TxnIssue)
            .GroupBy(t => t.Lot.ConsumableId)
            .Select(g => new { g.Key, Last = g.Max(x => x.TxnDate) })
            .ToDictionaryAsync(x => x.Key, x => x.Last, ct);
        DateOnly? LastIssued(int consumableId) => lastIssued.TryGetValue(consumableId, out var d) ? d : null;

        var totals = await ConsumableBalancesAsync(null, ct);
        var atLocation = aggs.GroupBy(a => a.ConsumableId).ToDictionary(g => g.Key, g => g.Sum(a => a.BalanceKg));

        var consumables = await db.Consumables.AsNoTracking()
            .Select(c => new { c.Id, c.ConsumableType, c.Manufacturer, c.Specification, c.Diameter, c.MinStockKg, c.IsActive })
            .ToListAsync(ct);

        var balances = consumables
            .Where(c => c.IsActive || atLocation.GetValueOrDefault(c.Id) != 0)
            .Select(c =>
            {
                var total = totals.GetValueOrDefault(c.Id);
                return new ConsumableBalanceRow(c.Id, c.ConsumableType, c.Manufacturer, Cat.DiaSpec(c.Diameter, c.Specification),
                    atLocation.GetValueOrDefault(c.Id), c.MinStockKg, Math.Max(0, c.MinStockKg - total),
                    LastIssued(c.Id), c.IsActive && IsLow(c.MinStockKg, total));
            })
            .OrderByDescending(r => r.BalanceKg).ThenBy(r => r.DiaSpec)
            .ToList();

        var lowStock = consumables
            .Where(c => c.IsActive && IsLow(c.MinStockKg, totals.GetValueOrDefault(c.Id)))
            .Select(c =>
            {
                var total = totals.GetValueOrDefault(c.Id);
                return new ConsumableBalanceRow(c.Id, c.ConsumableType, c.Manufacturer, Cat.DiaSpec(c.Diameter, c.Specification),
                    total, c.MinStockKg, c.MinStockKg - total, LastIssued(c.Id), true);
            })
            .OrderByDescending(r => r.ShortfallKg)
            .ToList();

        var recentQuery = db.ConsumableTransactions.AsNoTracking();
        if (loc is not null) recentQuery = recentQuery.Where(t => t.Location == loc);
        var recent = await Project(recentQuery.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id))
            .Take(30)
            .ToListAsync(ct);

        var current = inOut[^1];
        var kpis = new DashboardKpis(
            aggs.Sum(a => a.BalanceKg), current.InKg, current.OutKg, lowStock.Count,
            MonthLabel(monthStart), monthStart, today);

        return ServiceResult<DashboardDto>.Ok(new DashboardDto(kpis, inOut, consumption, balances, lowStock, recent));
    }

    public async Task<ServiceResult<ImportResult>> ImportOpeningAsync(Stream stream, CancellationToken ct)
    {
        string text;
        using (var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            text = await reader.ReadToEndAsync(ct);

        var rows = ParseCsv(text);
        if (rows.Count < 2) return ServiceResult<ImportResult>.Fail("The file has no data rows.");

        var header = rows[0].Select(HeaderKey).ToList();
        var missing = RequiredColumns.Where(c => !header.Contains(c.Key)).Select(c => c.Value).ToList();
        if (missing.Count > 0) return ServiceResult<ImportResult>.Fail($"Missing column(s): {string.Join(", ", missing)}.");

        string Cell(List<string> row, string key)
        {
            var index = header.IndexOf(key);
            return index >= 0 && index < row.Count ? row[index].Trim() : string.Empty;
        }

        var added = 0;
        var skipped = 0;
        var errors = new List<ImportError>();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await AcquireLockAsync(MasterLock, ct);

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var line = i + 1;
            if (row.All(string.IsNullOrWhiteSpace)) continue;

            var minText = Cell(row, "minstockkg");
            var (item, itemError) = Normalize(new ConsumableUpsert(
                Cell(row, "consumabletype"), Cell(row, "electrodebrand"), Cell(row, "electrodespecification"),
                Cell(row, "electrodediametermm"), minText.Length == 0 ? DefaultMinStockKg : ParseDecimal(minText) ?? -1));
            if (item is null) { errors.Add(new(line, itemError!)); continue; }

            var location = Cat.NormalizeLocation(Cell(row, "source"));
            if (location is null) { errors.Add(new(line, "Source must be Weldshop or Tool Crib.")); continue; }

            var lotNo = Trimmed(Cell(row, "lotheatnumber"));
            if (lotNo is null || lotNo.Length > 60) { errors.Add(new(line, "Lot\\ heat Number is required (max 60 characters).")); continue; }

            var qty = ParseDecimal(Cell(row, "receiveqtykg"));
            if (qty is null || qty <= 0) { errors.Add(new(line, "Receive Qty (KG) must be a number greater than 0.")); continue; }
            var receiveKg = RoundKg(qty.Value);

            var balanceText = Cell(row, "balance");
            var balance = balanceText.Length == 0 ? receiveKg : ParseDecimal(balanceText);
            if (balance is null || balance < 0 || balance > receiveKg)
            {
                errors.Add(new(line, "BALANCE must be a number between 0 and Receive Qty (KG)."));
                continue;
            }
            var balanceKg = RoundKg(balance.Value);

            var date = Today;
            var dateText = Cell(row, "date");
            if (dateText.Length > 0)
            {
                if (!DateOnly.TryParseExact(dateText, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    errors.Add(new(line, $"Date '{dateText}' is not a valid date (use d/M/yyyy)."));
                    continue;
                }
                date = parsed;
            }
            if (date > Today) { errors.Add(new(line, "Date cannot be in the future.")); continue; }

            var consumable = await FindOrCreateConsumableAsync(item, ct);
            await EnsureLookupsAsync(item, ct);
            var lot = await FindOrCreateLotAsync(consumable, lotNo, ct);

            if (lot.Id != 0 && await db.ConsumableTransactions.AnyAsync(t =>
                    t.LotId == lot.Id && t.Location == location && t.ReferenceNo == Cat.OpeningReference, ct))
            {
                await db.SaveChangesAsync(ct);
                skipped++;
                continue;
            }

            var requestor = FreeText(Cell(row, "requestor"), 200);
            db.ConsumableTransactions.Add(new ConsumableTransaction
            {
                Lot = lot,
                TxnType = Cat.TxnReceive,
                TxnDate = date,
                Location = location,
                QuantityKg = receiveKg,
                Requestor = requestor,
                ReferenceNo = Cat.OpeningReference,
                Remarks = "Imported from manual stock sheet",
            });
            if (balanceKg < receiveKg)
            {
                db.ConsumableTransactions.Add(new ConsumableTransaction
                {
                    Lot = lot,
                    TxnType = Cat.TxnIssue,
                    TxnDate = date,
                    Location = location,
                    QuantityKg = -(receiveKg - balanceKg),
                    Requestor = requestor ?? "Opening balance",
                    ReferenceNo = Cat.OpeningReference,
                    Remarks = "Takes recorded on the manual stock sheet before go-live",
                });
            }

            await db.SaveChangesAsync(ct);
            added++;
        }

        await tx.CommitAsync(ct);
        return ServiceResult<ImportResult>.Ok(new ImportResult(added, skipped, errors));
    }

    private IQueryable<ConsumableTransaction> Live()
        => db.ConsumableTransactions.AsNoTracking().Where(t => !t.IsVoided && t.TxnType != Cat.TxnVoid);

    private IQueryable<StockAgg> Aggregates(string? location)
    {
        var q = Live();
        if (location is not null) q = q.Where(t => t.Location == location);

        return q
            .GroupBy(t => new
            {
                t.LotId,
                t.Location,
                t.Lot.ConsumableId,
                t.Lot.LotNumber,
                t.Lot.Consumable.ConsumableType,
                t.Lot.Consumable.Manufacturer,
                t.Lot.Consumable.Specification,
                t.Lot.Consumable.Diameter,
                t.Lot.Consumable.MinStockKg,
            })
            .Select(g => new StockAgg(
                g.Key.LotId, g.Key.Location, g.Key.ConsumableId, g.Key.LotNumber, g.Key.ConsumableType,
                g.Key.Manufacturer, g.Key.Specification, g.Key.Diameter, g.Key.MinStockKg,
                g.Sum(x => x.TxnType == Cat.TxnReceive ? x.QuantityKg : 0m),
                g.Sum(x => x.TxnType == Cat.TxnIssue ? -x.QuantityKg : 0m),
                g.Sum(x => x.QuantityKg),
                g.Min(x => x.TxnDate),
                g.Max(x => x.CreatedAt)));
    }

    private static IQueryable<TransactionDto> Project(IQueryable<ConsumableTransaction> q)
        => q.Select(t => new TransactionDto(
            t.Id, t.TxnType, t.TxnDate, t.CreatedAt, t.Location, t.Lot.ConsumableId,
            t.Lot.Consumable.ConsumableType, t.Lot.Consumable.Manufacturer, t.Lot.Consumable.Specification,
            t.Lot.Consumable.Diameter, t.Lot.Consumable.Diameter + " " + t.Lot.Consumable.Specification,
            t.LotId, t.Lot.LotNumber, t.QuantityKg, t.Requestor, t.ReferenceNo, t.Remarks,
            t.IsVoided, t.VoidsTxnId));

    private async Task<decimal> BalanceAsync(int lotId, string location, CancellationToken ct)
        => await Live().Where(t => t.LotId == lotId && t.Location == location)
            .SumAsync(t => (decimal?)t.QuantityKg, ct) ?? 0m;

    private async Task<Dictionary<int, decimal>> ConsumableBalancesAsync(List<int>? ids, CancellationToken ct)
    {
        var q = Live();
        if (ids is not null) q = q.Where(t => ids.Contains(t.Lot.ConsumableId));
        return await q.GroupBy(t => t.Lot.ConsumableId)
            .Select(g => new { g.Key, Kg = g.Sum(x => x.QuantityKg) })
            .ToDictionaryAsync(x => x.Key, x => x.Kg, ct);
    }

    private async Task<MovementResult> MovementAsync(int txnId, int lotId, string location, CancellationToken ct)
    {
        var txn = await Project(db.ConsumableTransactions.AsNoTracking().Where(t => t.Id == txnId)).FirstAsync(ct);
        return new MovementResult(txn, await BalanceAsync(lotId, location, ct));
    }

    private async Task<Consumable> FindOrCreateConsumableAsync(NormalizedConsumable n, CancellationToken ct)
    {
        var existing = await db.Consumables.FirstOrDefaultAsync(c => c.ConsumableType == n.Type
            && c.Manufacturer == n.Manufacturer && c.Specification == n.Specification && c.Diameter == n.Diameter, ct);
        if (existing is not null) return existing;

        return db.Consumables.Add(new Consumable
        {
            ConsumableType = n.Type,
            Manufacturer = n.Manufacturer,
            Specification = n.Specification,
            Diameter = n.Diameter,
            MinStockKg = n.MinStockKg,
            IsActive = true,
        }).Entity;
    }

    private async Task<ConsumableLot> FindOrCreateLotAsync(Consumable consumable, string lotNo, CancellationToken ct)
    {
        if (consumable.Id != 0)
        {
            var lot = await db.ConsumableLots.FirstOrDefaultAsync(l => l.ConsumableId == consumable.Id && l.LotNumber == lotNo, ct);
            if (lot is not null) return lot;
        }
        return db.ConsumableLots.Add(new ConsumableLot { Consumable = consumable, LotNumber = lotNo }).Entity;
    }

    private async Task EnsureLookupsAsync(NormalizedConsumable n, CancellationToken ct)
    {
        (string Category, string Value)[] wanted =
        [
            (Cat.LookupManufacturer, n.Manufacturer),
            (Cat.LookupSize, n.Diameter),
            (Cat.LookupType, n.Specification),
        ];

        foreach (var (category, value) in wanted)
        {
            var pending = db.Lookups.Local.Any(l =>
                l.Category == category && string.Equals(l.Value, value, StringComparison.OrdinalIgnoreCase));
            if (pending || await db.Lookups.AnyAsync(l => l.Category == category && l.Value == value, ct)) continue;

            var maxOrder = await db.Lookups.Where(l => l.Category == category).MaxAsync(l => (int?)l.SortOrder, ct);
            var localMax = db.Lookups.Local.Where(l => l.Category == category).Select(l => (int?)l.SortOrder).Max();
            db.Lookups.Add(new LookupItem
            {
                Category = category,
                Value = value,
                SortOrder = Math.Max(maxOrder ?? -1, localMax ?? -1) + 1,
                IsActive = true,
            });
        }
    }

    private async Task AcquireLockAsync(string resource, CancellationToken ct)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var name = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
            new object[] { result, name }, ct);
        if (result.Value is not int code || code < 0)
            throw new TimeoutException("Another user is updating this stock right now. Please try again.");
    }

    private static string LotLock(int lotId, string location) => $"DCenter.Consumables.Lot.{lotId}.{location}";

    private static (NormalizedConsumable? Value, string? Error) Normalize(ConsumableUpsert? dto)
    {
        if (dto is null) return (null, "Consumable details are required.");

        var type = Cat.FindType(dto.ConsumableType);
        if (type is null) return (null, $"Consumable Type must be one of: {string.Join(", ", Cat.Types)}.");

        var manufacturer = Trimmed(dto.Manufacturer);
        if (manufacturer is null || manufacturer.Length > 100) return (null, "Electrode Brand is required (max 100 characters).");

        var specification = Trimmed(dto.Specification);
        if (specification is null || specification.Length > 100) return (null, "Electrode Specification is required (max 100 characters).");

        var diameter = Trimmed(dto.Diameter);
        if (diameter is null || diameter.Length > 30) return (null, "Electrode Diameter (mm) is required (max 30 characters).");

        if (dto.MinStockKg < 0) return (null, "Min stock cannot be negative.");

        return (new NormalizedConsumable(type, manufacturer, specification, diameter, RoundKg(dto.MinStockKg), dto.IsActive), null);
    }

    private static bool IsLow(decimal minStockKg, decimal totalKg) => minStockKg > 0 && totalKg <= minStockKg;

    private static decimal RoundKg(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static string? Trimmed(string? value)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static string? FreeText(string? value, int maxLength)
    {
        var v = Trimmed(value);
        return v is null ? null : v.Length <= maxLength ? v : v[..maxLength];
    }

    private static IEnumerable<string> Terms(string? q)
        => (q ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5);

    private static bool SameMonth(DateOnly date, DateOnly monthStart)
        => date.Year == monthStart.Year && date.Month == monthStart.Month;

    private static string MonthLabel(DateOnly monthStart) => monthStart.ToString("MMM yyyy", CultureInfo.InvariantCulture);

    private static decimal? ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static string HeaderKey(string header)
        => new(header.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (inQuotes)
            {
                if (ch != '"') cell.Append(ch);
                else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else inQuotes = false;
                continue;
            }

            switch (ch)
            {
                case '"': inQuotes = true; break;
                case ',': row.Add(cell.ToString()); cell.Clear(); break;
                case '\r': break;
                case '\n': row.Add(cell.ToString()); cell.Clear(); rows.Add(row); row = []; break;
                default: cell.Append(ch); break;
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
