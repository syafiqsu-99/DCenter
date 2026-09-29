using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

// Supervisor corrections: adjustments and voids.
public partial class ConsumableMovementService
{
    public async Task<ServiceResult<MovementResult>> AdjustAsync(AdjustRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = guards.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);
        if (guards.CheckBackdate(date) is string backdate) return Fail(backdate);

        var stage = T.Stage(r.Stage) ?? string.Empty;
        if (stage == Cat.Normal) return Fail("Normal storage is corrected through a Stock Count, not a single adjustment.");
        if (stage != Cat.Activated) return Fail("Adjustments are allowed for Activated storage.");

        var reason = T.Reason(r.Reason);
        if (reason is null) return Fail($"Reason must be one of: {string.Join(", ", Cat.AdjustReasons)}.");

        var remarks = T.FreeText(r.Remarks, 500);
        if (reason == Cat.ReasonOther && remarks is null) return Fail("Describe the reason in Remarks when choosing Other.");

        var counted = T.RoundKg(r.CountedQtyKg);
        if (counted < 0) return Fail("Counted quantity cannot be negative.");
        if (counted > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return ItemNotFound();

        if (r.LotId is int requested && !(await store.LotsAsync([requested], null, null, ct)).Any(l => l.ItemId == item.Id))
            return Fail("The selected lot does not belong to this consumable.");

        var (bin, binError) = G.ResolveBin(item, r.CompartmentId);
        if (binError is not null) return Fail(binError);

        var available = await guards.BinLotsAsync(item.Id, bin, ct);
        var current = r.LotId is int lotId
            ? available.Where(l => l.LotId == lotId).Sum(l => l.Available)
            : available.Sum(l => l.Available);
        var difference = counted - current;
        if (difference == 0) return Fail("The counted quantity matches the system balance, so there is nothing to adjust.");

        List<StockLine> lines;
        string? fromStage = null, toStage = null;
        int? fromBin = null, toBin = null;
        if (difference < 0)
        {
            fromStage = stage;
            fromBin = bin;
            if (r.LotId is int singleLot)
            {
                lines = [new StockLine(singleLot, -difference)];
            }
            else
            {
                var allocated = ConsumableLedger.AllocateFifo(available, -difference);
                if (allocated is null) return Fail("The balance changed while adjusting. Please try again.", StatusCodes.Status409Conflict);
                lines = allocated.Select(a => new StockLine(a.LotId, a.Kg)).ToList();
            }
        }
        else
        {
            toStage = stage;
            toBin = bin;
            if (bin is int compartmentId)
            {
                var compartmentError = await guards.CheckCompartmentAsync(item, compartmentId, ct);
                if (compartmentError is not null) return Fail(compartmentError, StatusCodes.Status409Conflict);
            }
            var target = r.LotId
                ?? available.Where(l => l.Available > 0).OrderByDescending(l => l.LotId).Select(l => (int?)l.LotId).FirstOrDefault()
                ?? (await store.LotsAsync(null, [item.Id], null, ct)).Select(l => (int?)l.Id).DefaultIfEmpty().Max();
            if (target is null) return Fail("Receive this consumable at least once before adjusting it.");
            lines = [new StockLine(target.Value, difference)];
        }

        var txnNo = await ledger.NextTxnNoAsync(ct);
        await store.AddMovementsAsync(lines.Select(line => new ConsumableMovement
        {
            TxnNo = txnNo,
            TxnType = Cat.TxnAdjust,
            TxnDate = date,
            LotId = line.LotId,
            QuantityKg = line.Kg,
            FromStage = fromStage,
            ToStage = toStage,
            FromCompartmentId = fromBin,
            ToCompartmentId = toBin,
            Reason = reason,
            CountedQtyKg = counted,
            Remarks = remarks,
            CreatedBy = user,
        }), ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    public async Task<ServiceResult<MovementResult>> VoidAsync(string txnNo, VoidRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, _, error) = guards.Common(enteredBy, null);
        if (error is not null) return Fail(error);

        var remarks = T.FreeText(r.Remarks, 500);
        if (remarks is null) return Fail("Remarks are required to void a transaction.");

        var headLines = await store.MovementsAsync(txnNo, false, null, ct);
        var headLots = (await store.LotsAsync(headLines.Select(m => m.LotId), null, null, ct)).ToDictionary(l => l.Id);
        var heads = headLines.Select(m => new { m.TxnType, m.IsVoided, headLots[m.LotId].ItemId }).ToList();
        if (heads.Count == 0) return Fail("Transaction not found.", StatusCodes.Status404NotFound);
        if (heads.Any(h => h.TxnType == Cat.TxnVoid)) return Fail("A void entry cannot itself be voided.");
        if (heads.All(h => h.IsVoided)) return Fail("This transaction has already been voided.", StatusCodes.Status409Conflict);

        var itemIds = heads.Select(h => h.ItemId).Distinct().Order().ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var itemId in itemIds) await StockLocks.AcquireAsync(db, StockLocks.Item(itemId), ct);

        var originals = await store.MovementsAsync(txnNo, true, null, ct);
        var originalLots = (await store.LotsAsync(originals.Select(o => o.LotId), null, null, ct)).ToDictionary(l => l.Id);
        foreach (var o in originals)
            o.Lot = new ConsumableItemLot { Id = o.LotId, ItemId = originalLots[o.LotId].ItemId, LotNumber = originalLots[o.LotId].LotNumber };
        if (originals.Count == 0) return Fail("This transaction has already been voided.", StatusCodes.Status409Conflict);

        var stages = (await ledger.LotStagesAsync(LedgerFilter.ForItems(itemIds), ct))
            .ToDictionary(l => l.LotId, l => l.Totals);
        foreach (var group in originals.Where(o => o.ToStage is not null).GroupBy(o => new { o.LotId, o.Lot.LotNumber, Stage = o.ToStage! }))
        {
            var needed = group.Sum(o => o.QuantityKg);
            var available = stages.GetValueOrDefault(group.Key.LotId)?.Of(group.Key.Stage) ?? 0m;
            if (needed > available)
                return Fail($"Voiding {txnNo} would leave {available - needed:0.00} kg of lot {group.Key.LotNumber} in " +
                            $"{group.Key.Stage} storage. Void the later entries for this lot first.", StatusCodes.Status409Conflict);
        }

        var bakingGroups = originals
            .Where(o => o.ToStage == Cat.Baking && o.BakingRecordId is not null)
            .GroupBy(o => o.BakingRecordId!.Value)
            .ToList();
        if (bakingGroups.Count > 0)
        {
            var recordBalances = await ledger.BakingBalancesAsync(bakingGroups.Select(g => g.Key).ToList(), ct);
            foreach (var group in bakingGroups)
            {
                var needed = group.Sum(o => o.QuantityKg);
                var available = recordBalances.GetValueOrDefault(group.Key);
                if (needed > available)
                    return Fail($"Voiding {txnNo} would leave its baking record at {available - needed:0.00} kg. " +
                                "Void the placements from that baking record first.", StatusCodes.Status409Conflict);
            }
        }

        foreach (var restore in originals
                     .Where(o => o.FromStage == Cat.Activated && o.FromCompartmentId is not null)
                     .Select(o => new { o.Lot.ItemId, CompartmentId = o.FromCompartmentId!.Value })
                     .Distinct()
                     .OrderBy(x => x.CompartmentId))
        {
            var restoreItem = await guards.ItemAsync(restore.ItemId, ct);
            if (restoreItem is null) return ItemNotFound();
            var compartmentError = await guards.CheckCompartmentAsync(restoreItem, restore.CompartmentId, ct);
            if (compartmentError is not null)
                return Fail($"Voiding {txnNo} would put stock back where it no longer fits: {compartmentError}", StatusCodes.Status409Conflict);
        }

        var bins = await ledger.ActivatedBinsAsync(LedgerFilter.ForItems(itemIds), ct);
        foreach (var group in originals.Where(o => o.ToStage == Cat.Activated).GroupBy(o => new { o.LotId, o.Lot.LotNumber, Bin = o.ToCompartmentId }))
        {
            var needed = group.Sum(o => o.QuantityKg);
            var available = bins.Where(b => b.LotId == group.Key.LotId && b.CompartmentId == group.Key.Bin).Sum(b => b.Kg);
            if (needed > available)
                return Fail($"Voiding {txnNo} would leave lot {group.Key.LotNumber} negative in " +
                            $"{await guards.BinNameAsync(group.Key.Bin, ct)}. Void the later entries for this lot first.",
                    StatusCodes.Status409Conflict);
        }

        var voidNo = await ledger.NextTxnNoAsync(ct);
        await store.SetVoidedAsync(originals.Select(o => o.Id), ct);
        await store.AddMovementsAsync(originals.Select(original => new ConsumableMovement
        {
            TxnNo = voidNo,
            TxnType = Cat.TxnVoid,
            TxnDate = original.TxnDate,
            LotId = original.LotId,
            QuantityKg = original.QuantityKg,
            FromStage = original.ToStage,
            ToStage = original.FromStage,
            FromCompartmentId = original.ToCompartmentId,
            ToCompartmentId = original.FromCompartmentId,
            BakingRecordId = original.BakingRecordId,
            Source = original.Source,
            Requestor = original.Requestor,
            WelderId = original.WelderId,
            Reason = original.Reason,
            ReferenceNo = original.TxnNo,
            Remarks = remarks,
            VoidsMovementId = original.Id,
            CreatedBy = user,
        }), ct);

        await store.VoidHoldingsAsync(txnNo, ct);

        var rebakeIds = originals
            .Where(o => o.TxnType == Cat.TxnReturn && o.ToStage == Cat.Baking && o.BakingRecordId is not null)
            .Select(o => o.BakingRecordId!.Value)
            .Distinct()
            .ToList();
        if (rebakeIds.Count > 0)
        {
            var records = await store.BakingRecordsAsync(rebakeIds, ct);
            foreach (var record in records)
            {
                record.RebakeStart = null;
                record.RebakeStop = null;
            }
            await store.SetBakingTimesAsync(records, ct);
        }

        foreach (var moved in originals
                     .Where(o => o.TxnType == Cat.TxnMove && o.FromCompartmentId is not null && o.ToCompartmentId is not null)
                     .GroupBy(o => new { o.Lot.ItemId, From = o.FromCompartmentId!.Value, To = o.ToCompartmentId!.Value }))
        {
            await RelocateHoldingsAsync(moved.Key.ItemId, moved.Select(o => o.LotId), moved.Key.To, moved.Key.From, ct);
        }

        var bakingIds = originals.Where(o => o.BakingRecordId is not null).Select(o => o.BakingRecordId!.Value).ToList();
        if (bakingIds.Count > 0) await ledger.RefreshBakingStatusAsync(bakingIds, ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(voidNo, itemIds[0], [], null, ct));
    }
}
