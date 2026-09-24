using DCenter.Server.Data;
using DCenter.Server.Entities;
using Microsoft.EntityFrameworkCore;
using Cat = DCenter.Server.Entities.StockCatalog;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public sealed record ItemRef(
    int Id, string Category, string DiaSpec, decimal? FinishThresholdKg, bool IsActive, string? HoldingOvenType)
{
    public bool IsElectrode => Category == Cat.ElectrodeFiller;
}

public sealed record WelderRef(int Id, string WelderName);

public sealed record StockLine(int LotId, decimal Kg);

public class ConsumableGuards(WeldReportContext db, ConsumableLedger ledger)
{
    public static (string? User, DateOnly Date, string? Error) Common(string? enteredBy, DateOnly? txnDate)
    {
        var user = T.FreeText(enteredBy, 100);
        if (user is null) return (null, default, "Select the welder or unlock Supervisor Mode before saving.");
        var date = txnDate ?? T.Today;
        if (date > T.Today) return (user, date, "Date cannot be in the future.");
        return (user, date, null);
    }

    public static (int? Bin, string? Error) ResolveBin(ItemRef item, int? compartmentId)
        => item.IsElectrode || compartmentId is null
            ? (compartmentId, null)
            : (null, "Bare & powder fillers are not stored in oven compartments.");

    public static (List<StockLine>? Lines, string? Error) Take(
        IEnumerable<(int LotId, decimal Available)> lots, int? lotId, decimal quantity, string where)
    {
        var list = lots.ToList();
        if (lotId is int id)
        {
            var available = list.Where(l => l.LotId == id).Sum(l => l.Available);
            return quantity <= available
                ? (new List<StockLine> { new(id, quantity) }, null)
                : (null, $"Only {Math.Max(available, 0m):0.00} kg of this lot is in {where}.");
        }

        var allocated = ConsumableLedger.AllocateFifo(list, quantity);
        if (allocated is not null) return (allocated.Select(a => new StockLine(a.LotId, a.Kg)).ToList(), null);

        var total = list.Sum(l => Math.Max(l.Available, 0m));
        return (null, $"Only {total:0.00} kg is in {where}.");
    }

    public Task<ItemRef?> ItemAsync(int id, CancellationToken ct)
        => db.ConsumableItems.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new ItemRef(i.Id, i.Category, i.Diameter + " " + i.Specification, i.FinishThresholdKg, i.IsActive,
                i.HoldingOvenType))
            .FirstOrDefaultAsync(ct);

    public async Task<(WelderRef? Welder, string? Error)> StockWelderAsync(int id, CancellationToken ct)
    {
        var w = await db.Welders.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new { x.Id, x.WelderName, x.IsActive, x.UsageScope })
            .FirstOrDefaultAsync(ct);
        if (w is null) return (null, "Welder not found.");
        if (!w.IsActive) return (null, $"{w.WelderName} is inactive.");
        if (w.UsageScope != WelderScope.ReportAndStock)
            return (null, $"{w.WelderName} is set up for Weld Report only. Change the scope to \"Report & Stock\" in Settings → Welders.");
        return (new WelderRef(w.Id, w.WelderName), null);
    }

    public async Task<List<(int LotId, decimal Available)>> BinLotsAsync(int itemId, int? bin, CancellationToken ct)
        => (await ledger.ActivatedBinsAsync(m => m.Lot.ItemId == itemId, ct))
            .Where(b => b.CompartmentId == bin)
            .Select(b => (b.LotId, b.Kg))
            .ToList();

    public async Task<string> BinNameAsync(int? bin, CancellationToken ct)
    {
        if (bin is not int id) return "Activated storage";
        var labels = await ledger.CompartmentLabelsAsync([id], ct);
        return $"compartment {labels.GetValueOrDefault(id, id.ToString())}";
    }

    public async Task<string?> CheckCompartmentAsync(ItemRef item, int compartmentId, CancellationToken ct)
    {
        await StockLocks.AcquireAsync(db, StockLocks.Compartment(compartmentId), ct);

        var c = await db.OvenCompartments.AsNoTracking()
            .Where(x => x.Id == compartmentId)
            .Select(x => new { x.Oven.OvenType, Label = x.Oven.Code + "-" + x.Label })
            .FirstOrDefaultAsync(ct);
        if (c is null) return "Compartment not found.";
        if (!item.IsElectrode) return "Only electrodes are kept in holding-oven compartments.";
        if (item.HoldingOvenType is null)
            return $"Set the holding oven type for {item.DiaSpec} under Supervisor → Consumables first.";
        if (c.OvenType != item.HoldingOvenType)
            return $"{item.DiaSpec} belongs in a {item.HoldingOvenType} oven, not {c.Label} ({c.OvenType}).";

        var occupiedByOther = (await ledger.ActivatedBinsAsync(
                m => m.ToCompartmentId == compartmentId || m.FromCompartmentId == compartmentId, ct))
            .Any(b => b.CompartmentId == compartmentId && b.Kg > 0 && b.ItemId != item.Id);
        return occupiedByOther
            ? $"{c.Label} already holds a different consumable. Choose an empty compartment or one holding {item.DiaSpec}."
            : null;
    }

    public async Task<string?> SameItemOtherLotWarningAsync(int itemId, int lotId, int compartmentId, CancellationToken ct)
    {
        var others = (await ledger.ActivatedBinsAsync(
                m => m.ToCompartmentId == compartmentId || m.FromCompartmentId == compartmentId, ct))
            .Where(b => b.CompartmentId == compartmentId && b.Kg > 0 && b.ItemId == itemId && b.LotId != lotId)
            .Select(b => b.LotId)
            .ToList();
        if (others.Count == 0) return null;
        var lotNumbers = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => others.Contains(l.Id))
            .Select(l => l.LotNumber)
            .ToListAsync(ct);
        return $"This compartment also holds lot {string.Join(", ", lotNumbers)} of the same consumable.";
    }

    public async Task<decimal> OutstandingAsync(int welderId, int itemId, DateOnly since, DateOnly until, CancellationToken ct)
    {
        var window = ledger.Live()
            .Where(m => m.WelderId == welderId && m.Lot.ItemId == itemId && m.TxnDate >= since && m.TxnDate <= until);
        var picked = await window.Where(m => m.TxnType == Cat.TxnIssue).SumAsync(m => (decimal?)m.QuantityKg, ct) ?? 0m;
        var returned = await window.Where(m => m.TxnType == Cat.TxnReturn).SumAsync(m => (decimal?)m.QuantityKg, ct) ?? 0m;
        return picked - returned;
    }
}
