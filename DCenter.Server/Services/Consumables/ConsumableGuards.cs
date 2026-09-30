using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public sealed record ItemRef(
    int Id, string Category, string DiaSpec, decimal? FinishThresholdKg, bool IsActive, string? HoldingOvenType)
{
    public bool IsElectrode => Category == Cat.ElectrodeFiller;
}

public sealed record WelderRef(int Id, string WelderName);

public sealed record ReceiverRef(string WelderName, bool IsActive);

public sealed record StockLine(int LotId, decimal Kg);

public class ConsumableGuards(
    WeldReportContext db, StoredProcedures sp, ConsumableStore store, ConsumableLedger ledger, ISupervisorContext supervisor,
    IOptions<ConsumableOptions> options, TimeProvider time)
{
    public static string? BackdateError(DateOnly date, DateOnly today, bool supervisor, int days)
    {
        if (supervisor || date >= today.AddDays(-Math.Max(days, 0))) return null;
        return days == 0
            ? "Welders can only record today's entries. Ask a supervisor to record older ones."
            : $"Welders can record entries up to {days} day(s) back. Ask a supervisor to record older ones.";
    }

    // Supervisors may back-date freely; everyone else is limited to the configured window.
    public string? CheckBackdate(DateOnly date)
        => BackdateError(date, Today, supervisor.IsSupervisor, options.Value.WelderBackdateDays);

    public DateOnly Today => time.Today();

    public (string? User, DateOnly Date, string? Error) Common(string? enteredBy, DateOnly? txnDate)
        => Common(enteredBy, txnDate, Today);

    public static (string? User, DateOnly Date, string? Error) Common(string? enteredBy, DateOnly? txnDate, DateOnly today)
    {
        var user = T.FreeText(enteredBy, 100);
        if (user is null) return (null, default, "Select the welder or unlock Supervisor Mode before saving.");
        var date = txnDate ?? today;
        if (date > today) return (user, date, "Date cannot be in the future.");
        return (user, date, null);
    }

    public static (string? Name, string? Error) ReceiverName(int? welderId, ReceiverRef? welder)
    {
        if (welderId is null) return (null, "Choose who received this stock in Received By.");
        if (welder is null) return (null, "The person in Received By is no longer in the welder list. Choose another name.");
        if (!welder.IsActive)
            return (null, $"{welder.WelderName} is not an active welder. Choose another name or reactivate them in Settings → Welders.");
        return (welder.WelderName, null);
    }

    public async Task<ReceiverRef?> ReceiverAsync(int? welderId, CancellationToken ct)
        => welderId is int id && await WelderAsync(id, ct) is { } w ? new ReceiverRef(w.WelderName, w.IsActive) : null;

    private Task<WelderDto?> WelderAsync(int id, CancellationToken ct)
        => sp.FirstOrDefaultAsync<WelderDto>("SP_Welder_List", ct, Sql.Int("@Id", id));

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

    public async Task<ItemRef?> ItemAsync(int id, CancellationToken ct)
        => await store.ItemAsync(id, ct) is { } i
            ? new ItemRef(i.Id, i.Category, i.Diameter + " " + i.Specification, i.FinishThresholdKg, i.IsActive, i.HoldingOvenType)
            : null;

    public async Task<(WelderRef? Welder, string? Error)> StockWelderAsync(int id, CancellationToken ct)
    {
        var w = await WelderAsync(id, ct);
        if (w is null) return (null, "Welder not found.");
        if (!w.IsActive) return (null, $"{w.WelderName} is inactive.");
        if (w.UsageScope != WelderScope.ReportAndStock)
            return (null, $"{w.WelderName} is set up for Weld Report only. Change the scope to \"Report & Stock\" in Settings → Welders.");
        return (new WelderRef(w.Id, w.WelderName), null);
    }

    public async Task<List<(int LotId, decimal Available)>> BinLotsAsync(int itemId, int? bin, CancellationToken ct)
        => (await ledger.ActivatedBinsAsync(LedgerFilter.ForItem(itemId), ct))
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

        var c = (await store.CompartmentsAsync([compartmentId], ct)).FirstOrDefault();
        if (c is null) return "Compartment not found.";
        if (!item.IsElectrode) return "Only electrodes are kept in holding-oven compartments.";
        if (item.HoldingOvenType is null)
            return $"Set the holding oven type for {item.DiaSpec} under Supervisor → Consumables first.";
        if (c.OvenType != item.HoldingOvenType)
            return $"{item.DiaSpec} belongs in a {item.HoldingOvenType} oven, not {c.Label} ({c.OvenType}).";

        var occupiedByOther = (await ledger.ActivatedBinsAsync(new LedgerFilter(CompartmentId: compartmentId), ct))
            .Any(b => b.CompartmentId == compartmentId && b.Kg > 0 && b.ItemId != item.Id);
        return occupiedByOther
            ? $"{c.Label} already holds a different consumable. Choose an empty compartment or one holding {item.DiaSpec}."
            : null;
    }

    public async Task<string?> SameItemOtherLotWarningAsync(int itemId, int lotId, int compartmentId, CancellationToken ct)
    {
        var others = (await ledger.ActivatedBinsAsync(new LedgerFilter(CompartmentId: compartmentId), ct))
            .Where(b => b.CompartmentId == compartmentId && b.Kg > 0 && b.ItemId == itemId && b.LotId != lotId)
            .Select(b => b.LotId)
            .ToList();
        if (others.Count == 0) return null;
        var lotNumbers = (await store.LotsAsync(others, null, null, ct)).Select(l => l.LotNumber).ToList();
        return $"This compartment also holds lot {string.Join(", ", lotNumbers)} of the same consumable.";
    }

    public async Task<decimal> OutstandingAsync(int welderId, int itemId, DateOnly since, DateOnly until, CancellationToken ct)
        => (await ledger.WelderAsync(welderId, since, until, itemId, null, ct)).Sum(w => w.Picked - w.Returned);
}
