using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Cat = DCenter.Server.Entities.StockCatalog;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public sealed record ItemInput(
    string Category, string Specification, string Diameter, decimal MinStockKg, decimal ActivatedMinKg,
    decimal? FinishThresholdKg, bool IsActive, string? HoldingOvenType);

public class ConsumableItemService(WeldReportContext db, ConsumableStore store, ConsumableLedger ledger)
{
    public const string LookupBrand = "Manuf";
    public const string LookupSize = "Size";
    public const string LookupType = "Type";

    public async Task<Dictionary<string, string>> SpecificationNamesAsync(CancellationToken ct)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in await store.SpecificationNamesAsync(LookupType, ct))
            if (T.Collapse(value) is string collapsed) names.TryAdd(collapsed, collapsed);
        return names;
    }

    public (ItemInput? Value, string? Error) Normalize(ItemUpsert? dto, IReadOnlyDictionary<string, string> specifications)
    {
        if (dto is null) return (null, "Consumable details are required.");

        var category = T.Category(dto.Category);
        if (category is null)
            return (null, T.OptionError("Consumable Type", dto.Category, Cat.Categories, T.MatchOption(dto.Category, Cat.Categories).Suggestion));

        var specification = T.Collapse(dto.Specification) is string typed ? specifications.GetValueOrDefault(typed, typed) : null;
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

    // Returns the existing consumable, or adds a new one (inside the caller's transaction).
    public async Task<(ConsumableItem? Item, string? Error)> FindOrCreateAsync(ItemInput n, CancellationToken ct)
    {
        var existing = await store.FindItemAsync(n.Specification, n.Diameter, 0, ct);
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
        await store.SaveItemAsync(item, ct);
        return (item, null);
    }

    public async Task<ServiceResult<ItemDto>> UpsertAsync(int? id, ItemUpsert dto, CancellationToken ct)
    {
        var (n, error) = Normalize(dto, await SpecificationNamesAsync(ct));
        if (n is null) return ServiceResult<ItemDto>.Fail(error!);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        var currentId = id ?? 0;
        if (await store.FindItemAsync(n.Specification, n.Diameter, currentId, ct) is not null)
            return ServiceResult<ItemDto>.Fail(
                $"{Cat.DiaSpec(n.Diameter, n.Specification)} already exists.", StatusCodes.Status409Conflict);

        ConsumableItem? item;
        if (id is int existingId)
        {
            item = await store.ItemAsync(existingId, ct);
            if (item is null) return ServiceResult<ItemDto>.Fail("Consumable not found.", StatusCodes.Status404NotFound);

            var changesIdentity = item.Category != n.Category || item.Diameter != n.Diameter
                || !string.Equals(item.Specification, n.Specification, StringComparison.OrdinalIgnoreCase);
            if (changesIdentity && (await store.ItemHistoryAsync(existingId, ct)).Movements > 0)
                return ServiceResult<ItemDto>.Fail(
                    "Type, specification and diameter cannot change once stock has been recorded. Create a new consumable instead.");

            if (item.HoldingOvenType != n.HoldingOvenType
                && (await ledger.ActivatedBinsAsync(LedgerFilter.ForItem(existingId), ct)).Any(b => b.CompartmentId is not null && b.Kg > 0))
                return ServiceResult<ItemDto>.Fail(
                    "The holding oven type cannot change while this consumable is in oven compartments. Move or finish it first.",
                    StatusCodes.Status409Conflict);
        }
        else
        {
            item = new ConsumableItem();
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
        await store.SaveItemAsync(item, ct);
        await tx.CommitAsync(ct);

        var totals = (await ledger.ItemTotalsAsync([item.Id], ct)).GetValueOrDefault(item.Id) ?? StageTotals.Zero;
        return ServiceResult<ItemDto>.Ok(ToDto(item.Id, item.Category, item.Specification, item.Diameter, item.MinStockKg,
            item.ActivatedMinKg, item.FinishThresholdKg, item.IsActive, item.HoldingOvenType, totals));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        var item = await store.ItemAsync(id, ct);
        if (item is null) return ServiceResult<bool>.Fail("Consumable not found.", StatusCodes.Status404NotFound);

        var (movements, bakings) = await store.ItemHistoryAsync(id, ct);
        if (movements + bakings > 0)
            return ServiceResult<bool>.Fail(
                $"{Cat.DiaSpec(item.Diameter, item.Specification)} has stock history ({movements} transaction line(s), {bakings} baking record(s)) " +
                "and can't be deleted. Set it inactive instead.", StatusCodes.Status409Conflict);

        await store.DeleteItemAsync(id, ct);
        await tx.CommitAsync(ct);
        return ServiceResult<bool>.Ok(true);
    }

    public async Task<List<ItemDto>> SearchAsync(string? q, string? category, bool activeOnly, int take, CancellationToken ct)
    {
        var items = await store.SearchItemsAsync(T.Terms(q), category, activeOnly, Math.Clamp(take, 1, 2000), ct);

        var totals = await ledger.ItemTotalsAsync(items.Select(i => i.Id).ToList(), ct);
        return items
            .OrderBy(i => i.Category).ThenBy(i => i.Specification).ThenBy(i => T.DiameterSortKey(i.Diameter))
            .Select(i => ToDto(i.Id, i.Category, i.Specification, i.Diameter, i.MinStockKg, i.ActivatedMinKg,
                i.FinishThresholdKg, i.IsActive, i.HoldingOvenType, totals.GetValueOrDefault(i.Id) ?? StageTotals.Zero))
            .ToList();
    }

    // Adds the values missing from the dropdown lists (inside the caller's transaction).
    public async Task EnsureLookupsAsync(IEnumerable<(string Category, string Value)> wanted, CancellationToken ct)
    {
        var requested = wanted
            .Where(w => !string.IsNullOrWhiteSpace(w.Value))
            .DistinctBy(w => (w.Category, w.Value.ToUpperInvariant()))
            .ToList();
        if (requested.Count == 0) return;

        var added = new List<LookupItem>();
        foreach (var group in requested.GroupBy(w => w.Category))
        {
            var stored = await store.LookupsAsync(group.Key, ct);
            var known = stored.Select(l => l.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var next = stored.Select(l => l.SortOrder).DefaultIfEmpty(-1).Max() + 1;

            foreach (var (_, value) in group.Where(w => !known.Contains(w.Value)))
            {
                added.Add(new LookupItem { Category = group.Key, Value = value, SortOrder = next++, IsActive = true });
                known.Add(value);
            }
        }
        await store.AddLookupsAsync(added, ct);
    }

    private static ItemDto ToDto(
        int id, string category, string specification, string diameter, decimal minStockKg, decimal activatedMinKg,
        decimal? finishThresholdKg, bool isActive, string? holdingOvenType, StageTotals totals)
        => new(id, category, specification, diameter, Cat.DiaSpec(diameter, specification), minStockKg, activatedMinKg,
            finishThresholdKg, isActive, totals.NormalKg, totals.ActivatedKg, totals.TotalKg, holdingOvenType);
}
