using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Cat = DCenter.Server.Entities.StockCatalog;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public sealed record ItemInput(
    string Category, string Specification, string Diameter, decimal MinStockKg, decimal ActivatedMinKg,
    decimal? FinishThresholdKg, bool IsActive, string? HoldingOvenType);

public class ConsumableItemService(WeldReportContext db, ConsumableLedger ledger)
{
    public const string LookupBrand = "Manuf";
    public const string LookupSize = "Size";
    public const string LookupType = "Type";

    public (ItemInput? Value, string? Error) Normalize(ItemUpsert? dto)
    {
        if (dto is null) return (null, "Consumable details are required.");

        var category = T.Category(dto.Category);
        if (category is null)
            return (null, T.OptionError("Consumable Type", dto.Category, Cat.Categories, T.MatchOption(dto.Category, Cat.Categories).Suggestion));

        var specification = T.Specification(dto.Specification);
        if (specification is null || specification.Length > 100)
            return (null, "Electrode Specification is required (max 100 characters).");

        var diameter = T.Diameter(dto.Diameter, category);
        if (diameter is null)
            return (null, category == Cat.ElectrodeFiller
                ? "Electrode Diameter (mm) must be a number such as 2.40 or 3.20."
                : "Diameter must be a number such as 1.20, or a mesh size such as 80/325.");

        if (dto.MinStockKg < 0 || dto.ActivatedMinKg < 0) return (null, "Minimum quantities cannot be negative.");
        if (dto.FinishThresholdKg is < 0 or > 50) return (null, "Finish threshold must be between 0 and 50 kg.");

        string? ovenType = null;
        if (category == Cat.ElectrodeFiller && T.Trimmed(dto.HoldingOvenType) is string rawOven)
        {
            var (matched, suggestion) = T.MatchOption(rawOven, Cat.OvenTypes);
            if (matched is null) return (null, T.OptionError("Holding Oven", rawOven, Cat.OvenTypes, suggestion));
            ovenType = matched;
        }

        return (new ItemInput(category, specification, diameter, T.RoundKg(dto.MinStockKg), T.RoundKg(dto.ActivatedMinKg),
            dto.FinishThresholdKg is decimal f ? T.RoundKg(f) : null, dto.IsActive, ovenType), null);
    }

    public async Task<(ConsumableItem? Item, string? Error)> FindOrCreateAsync(ItemInput n, CancellationToken ct)
    {
        var existing = await db.ConsumableItems
            .FirstOrDefaultAsync(i => i.Specification == n.Specification && i.Diameter == n.Diameter, ct);
        if (existing is not null)
        {
            if (existing.Category != n.Category)
                return (null, $"{Cat.DiaSpec(n.Diameter, n.Specification)} already exists as {existing.Category}.");
            return existing.IsActive
                ? (existing, null)
                : (null, $"{Cat.DiaSpec(n.Diameter, n.Specification)} exists but is inactive. Reactivate it under Consumables first.");
        }

        var item = new ConsumableItem
        {
            Category = n.Category,
            Specification = n.Specification,
            Diameter = n.Diameter,
            MinStockKg = n.MinStockKg,
            ActivatedMinKg = n.ActivatedMinKg,
            FinishThresholdKg = n.FinishThresholdKg,
            HoldingOvenType = n.HoldingOvenType,
            IsActive = true,
        };
        db.ConsumableItems.Add(item);
        return (item, null);
    }

    public async Task<ServiceResult<ItemDto>> UpsertAsync(int? id, ItemUpsert dto, CancellationToken ct)
    {
        var (n, error) = Normalize(dto);
        if (n is null) return ServiceResult<ItemDto>.Fail(error!);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        var currentId = id ?? 0;
        if (await db.ConsumableItems.AnyAsync(i => i.Id != currentId
                && i.Specification == n.Specification && i.Diameter == n.Diameter, ct))
            return ServiceResult<ItemDto>.Fail(
                $"{Cat.DiaSpec(n.Diameter, n.Specification)} already exists.", StatusCodes.Status409Conflict);

        ConsumableItem? item;
        if (id is int existingId)
        {
            item = await db.ConsumableItems.FirstOrDefaultAsync(i => i.Id == existingId, ct);
            if (item is null) return ServiceResult<ItemDto>.Fail("Consumable not found.", StatusCodes.Status404NotFound);

            var changesIdentity = item.Category != n.Category || item.Specification != n.Specification || item.Diameter != n.Diameter;
            if (changesIdentity && await db.ConsumableMovements.AnyAsync(m => m.Lot.ItemId == existingId, ct))
                return ServiceResult<ItemDto>.Fail(
                    "Type, specification and diameter cannot change once stock has been recorded. Create a new consumable instead.");

            if (item.HoldingOvenType != n.HoldingOvenType
                && (await ledger.ActivatedBinsAsync(m => m.Lot.ItemId == existingId, ct)).Any(b => b.CompartmentId is not null && b.Kg > 0))
                return ServiceResult<ItemDto>.Fail(
                    "The holding oven type cannot change while this consumable is in oven compartments. Move or finish it first.",
                    StatusCodes.Status409Conflict);
        }
        else
        {
            item = new ConsumableItem();
            db.ConsumableItems.Add(item);
        }

        item.Category = n.Category;
        item.Specification = n.Specification;
        item.Diameter = n.Diameter;
        item.MinStockKg = n.MinStockKg;
        item.ActivatedMinKg = n.ActivatedMinKg;
        item.FinishThresholdKg = n.FinishThresholdKg;
        item.HoldingOvenType = n.HoldingOvenType;
        item.IsActive = n.IsActive;

        await EnsureLookupsAsync([(LookupSize, item.Diameter), (LookupType, item.Specification)], ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var totals = (await ledger.ItemTotalsAsync([item.Id], ct)).GetValueOrDefault(item.Id) ?? StageTotals.Zero;
        return ServiceResult<ItemDto>.Ok(ToDto(item.Id, item.Category, item.Specification, item.Diameter, item.MinStockKg,
            item.ActivatedMinKg, item.FinishThresholdKg, item.IsActive, item.HoldingOvenType, totals));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        var item = await db.ConsumableItems.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return ServiceResult<bool>.Fail("Consumable not found.", StatusCodes.Status404NotFound);

        var movements = await db.ConsumableMovements.CountAsync(m => m.Lot.ItemId == id, ct);
        var bakings = await db.BakingRecords.CountAsync(b => b.Lot.ItemId == id, ct);
        if (movements + bakings > 0)
            return ServiceResult<bool>.Fail(
                $"{Cat.DiaSpec(item.Diameter, item.Specification)} has stock history ({movements} transaction line(s), {bakings} baking record(s)) " +
                "and can't be deleted. Set it inactive instead.", StatusCodes.Status409Conflict);

        db.ConsumableItemLots.RemoveRange(await db.ConsumableItemLots.Where(l => l.ItemId == id).ToListAsync(ct));
        db.ConsumableItems.Remove(item);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<List<ItemDto>> SearchAsync(string? q, string? category, bool activeOnly, int take, CancellationToken ct)
    {
        var query = db.ConsumableItems.AsNoTracking();
        if (activeOnly) query = query.Where(i => i.IsActive);
        if (category is not null) query = query.Where(i => i.Category == category);
        foreach (var term in T.Terms(q))
            query = query.Where(i => i.Specification.Contains(term) || i.Diameter.Contains(term) || i.Category.Contains(term));

        var items = await query
            .OrderBy(i => i.Category).ThenBy(i => i.Specification).ThenBy(i => i.Diameter).ThenBy(i => i.Id)
            .Take(Math.Clamp(take, 1, 2000))
            .Select(i => new
            {
                i.Id, i.Category, i.Specification, i.Diameter, i.MinStockKg, i.ActivatedMinKg, i.FinishThresholdKg, i.IsActive,
                i.HoldingOvenType,
            })
            .ToListAsync(ct);

        var totals = await ledger.ItemTotalsAsync(items.Select(i => i.Id).ToList(), ct);
        return items
            .OrderBy(i => i.Category).ThenBy(i => i.Specification).ThenBy(i => T.DiameterSortKey(i.Diameter))
            .Select(i => ToDto(i.Id, i.Category, i.Specification, i.Diameter, i.MinStockKg, i.ActivatedMinKg,
                i.FinishThresholdKg, i.IsActive, i.HoldingOvenType, totals.GetValueOrDefault(i.Id) ?? StageTotals.Zero))
            .ToList();
    }

    public async Task EnsureLookupsAsync(IEnumerable<(string Category, string Value)> wanted, CancellationToken ct)
    {
        var requested = wanted
            .Where(w => !string.IsNullOrWhiteSpace(w.Value))
            .DistinctBy(w => (w.Category, w.Value.ToUpperInvariant()))
            .ToList();
        if (requested.Count == 0) return;

        var categories = requested.Select(w => w.Category).Distinct().ToList();
        var stored = await db.Lookups.AsNoTracking()
            .Where(l => categories.Contains(l.Category))
            .Select(l => new { l.Category, l.Value, l.SortOrder })
            .ToListAsync(ct);
        var pending = db.Lookups.Local.Where(l => categories.Contains(l.Category)).ToList();

        foreach (var group in requested.GroupBy(w => w.Category))
        {
            var known = stored.Where(l => l.Category == group.Key).Select(l => l.Value)
                .Concat(pending.Where(l => l.Category == group.Key).Select(l => l.Value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var next = stored.Where(l => l.Category == group.Key).Select(l => l.SortOrder)
                .Concat(pending.Where(l => l.Category == group.Key).Select(l => l.SortOrder))
                .DefaultIfEmpty(-1).Max() + 1;

            foreach (var (_, value) in group.Where(w => !known.Contains(w.Value)))
            {
                db.Lookups.Add(new LookupItem { Category = group.Key, Value = value, SortOrder = next++, IsActive = true });
                known.Add(value);
            }
        }
    }

    private static ItemDto ToDto(
        int id, string category, string specification, string diameter, decimal minStockKg, decimal activatedMinKg,
        decimal? finishThresholdKg, bool isActive, string? holdingOvenType, StageTotals totals)
        => new(id, category, specification, diameter, Cat.DiaSpec(diameter, specification), minStockKg, activatedMinKg,
            finishThresholdKg, isActive, totals.NormalKg, totals.ActivatedKg, totals.TotalKg, holdingOvenType);
}
