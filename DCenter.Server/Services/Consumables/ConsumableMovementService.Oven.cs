using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

// Moving electrodes between holding-oven compartments.
public partial class ConsumableMovementService
{
    public async Task<ServiceResult<MovementResult>> MoveAsync(MoveRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = guards.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);
        if (guards.CheckBackdate(date) is string backdate) return Fail(backdate);
        if (r.FromCompartmentId == r.ToCompartmentId) return Fail("Choose a different compartment to move to.");

        var qty = T.RoundKg(r.QuantityKg);
        if (!r.TakeAll && qty <= 0) return Fail("Quantity must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return ItemNotFound();
        if (!item.IsElectrode) return Fail("Only electrodes are moved between oven compartments.");

        var compartmentError = await guards.CheckCompartmentAsync(item, r.ToCompartmentId, ct);
        if (compartmentError is not null) return Fail(compartmentError, StatusCodes.Status409Conflict);

        var binLots = await guards.BinLotsAsync(item.Id, r.FromCompartmentId, ct);
        var where = await guards.BinNameAsync(r.FromCompartmentId, ct);
        List<StockLine> lines;
        if (r.TakeAll)
        {
            lines = binLots
                .Where(l => (r.LotId is null || l.LotId == r.LotId) && l.Available > 0)
                .Select(l => new StockLine(l.LotId, l.Available))
                .ToList();
            if (lines.Count == 0) return Fail($"There is no {item.DiaSpec} in {where}.", StatusCodes.Status409Conflict);
        }
        else
        {
            var (planned, takeError) = G.Take(binLots, r.LotId, qty, where);
            if (planned is null) return Fail(takeError!, StatusCodes.Status409Conflict);
            lines = planned;
        }

        var txnNo = await ledger.NextTxnNoAsync(ct);
        await store.AddMovementsAsync(lines.Select(line => new ConsumableMovement
        {
            TxnNo = txnNo,
            TxnType = Cat.TxnMove,
            TxnDate = date,
            LotId = line.LotId,
            QuantityKg = line.Kg,
            FromStage = Cat.Activated,
            ToStage = Cat.Activated,
            FromCompartmentId = r.FromCompartmentId,
            ToCompartmentId = r.ToCompartmentId,
            Remarks = T.FreeText(r.Remarks, 500),
            CreatedBy = user,
        }), ct);

        if (r.FromCompartmentId is int fromId)
            await RelocateHoldingsAsync(item.Id, lines.Select(l => l.LotId), fromId, r.ToCompartmentId, ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    private async Task RelocateHoldingsAsync(int itemId, IEnumerable<int> lotIds, int fromId, int toId, CancellationToken ct)
    {
        var remaining = (await guards.BinLotsAsync(itemId, fromId, ct))
            .GroupBy(l => l.LotId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Available));
        var emptied = lotIds.Distinct().Where(id => remaining.GetValueOrDefault(id) <= 0).ToList();
        if (emptied.Count == 0) return;

        await store.RelocateHoldingsAsync(fromId, toId, emptied, ct);
    }
}
