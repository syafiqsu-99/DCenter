using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public class ConsumableMovementService(
    WeldReportContext db, ConsumableLedger ledger, ConsumableItemService items, ConsumableGuards guards,
    IOptions<ConsumableOptions> options)
{
    private readonly ConsumableOptions settings = options.Value;

    public async Task<ServiceResult<MovementResult>> ReceiveAsync(ReceiveRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);

        var source = T.Source(r.Source);
        if (source is null) return Fail("Source must be Weld Shop or Tool Crib.");

        var qty = T.RoundKg(r.QuantityKg);
        if (qty <= 0) return Fail("Receive Qty (KG) must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        var brand = T.FreeText(r.Brand, 100);
        if (brand is null) return Fail("Electrode Brand is required.");

        var lotNo = T.Collapse(r.LotNumber);
        if (lotNo is null) return Fail("Lot\\ heat Number is required.");
        if (lotNo.Length > 60) return Fail("Lot\\ heat Number is limited to 60 characters.");

        ItemInput? newItem = null;
        if (r.ItemId is null)
        {
            var (n, itemError) = items.Normalize(r.NewItem);
            if (n is null) return Fail(itemError!);
            newItem = n;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        ConsumableItem? item;
        if (r.ItemId is int itemId)
        {
            item = await db.ConsumableItems.FirstOrDefaultAsync(i => i.Id == itemId, ct);
            if (item is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);
            if (!item.IsActive) return Fail("This consumable is inactive. Reactivate it in Settings before receiving stock.");
        }
        else
        {
            var (created, createError) = await items.FindOrCreateAsync(newItem!, ct);
            if (created is null) return Fail(createError!, StatusCodes.Status409Conflict);
            item = created;
        }

        await items.EnsureLookupsAsync(
        [
            (ConsumableItemService.LookupBrand, brand),
            (ConsumableItemService.LookupSize, T.FormatDiameter(item.Diameter)),
            (ConsumableItemService.LookupType, item.Specification),
        ], ct);

        var lot = item.Id == 0
            ? null
            : await db.ConsumableItemLots.FirstOrDefaultAsync(l => l.ItemId == item.Id && l.Brand == brand && l.LotNumber == lotNo, ct);
        lot ??= db.ConsumableItemLots.Add(new ConsumableItemLot { Item = item, Brand = brand, LotNumber = lotNo }).Entity;

        var txnNo = await ledger.NextTxnNoAsync(ct);
        db.ConsumableMovements.Add(new ConsumableMovement
        {
            TxnNo = txnNo,
            TxnType = Cat.TxnReceive,
            TxnDate = date,
            Lot = lot,
            QuantityKg = qty,
            ToStage = Cat.Normal,
            Source = source,
            Requestor = T.FreeText(r.ReceivedBy, 200) ?? user,
            Remarks = T.FreeText(r.Remarks, 500),
            CreatedBy = user,
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    public async Task<ServiceResult<MovementResult>> TransferAsync(TransferRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);

        var from = T.Stage(r.FromStage);
        var to = T.Stage(r.ToStage);
        if (!((from == Cat.Normal && to == Cat.Activated) || (from == Cat.Activated && to == Cat.Normal)))
            return Fail("Transfers move stock between Normal and Activated storage.");

        var qty = T.RoundKg(r.QuantityKg);
        if (qty <= 0) return Fail("Quantity must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);
        if (item.IsElectrode && !settings.AllowElectrodeDirectTransfer)
            return Fail("Electrodes reach Activated storage through Baking and the holding ovens, not by direct transfer.");

        var available = from == Cat.Normal
            ? (await ledger.LotStagesForItemAsync(item.Id, ct)).Select(l => (LotId: l.LotId, Available: l.Totals.NormalKg)).ToList()
            : await guards.BinLotsAsync(item.Id, null, ct);
        var (lines, takeError) = G.Take(available, r.LotId, qty, $"{from} storage");
        if (lines is null) return Fail(takeError!, StatusCodes.Status409Conflict);

        var txnNo = await ledger.NextTxnNoAsync(ct);
        foreach (var line in lines)
        {
            db.ConsumableMovements.Add(new ConsumableMovement
            {
                TxnNo = txnNo,
                TxnType = Cat.TxnTransfer,
                TxnDate = date,
                LotId = line.LotId,
                QuantityKg = line.Kg,
                FromStage = from,
                ToStage = to,
                Remarks = T.FreeText(r.Remarks, 500),
                CreatedBy = user,
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    public async Task<ServiceResult<MovementResult>> IssueAsync(IssueRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);

        var qty = T.RoundKg(r.QuantityKg);
        if (!r.TakeAll && qty <= 0) return Fail("Take/KG must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var (welder, welderError) = await guards.StockWelderAsync(r.WelderId, ct);
        if (welder is null) return Fail(welderError!);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);

        var (bin, binError) = G.ResolveBin(item, r.CompartmentId);
        if (binError is not null) return Fail(binError);

        var binLots = await guards.BinLotsAsync(item.Id, bin, ct);
        var where = await guards.BinNameAsync(bin, ct);
        List<StockLine> lines;
        if (r.TakeAll)
        {
            lines = binLots
                .Where(l => (r.LotId is null || l.LotId == r.LotId) && l.Available > 0)
                .OrderBy(l => l.LotId)
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
        foreach (var line in lines)
        {
            db.ConsumableMovements.Add(new ConsumableMovement
            {
                TxnNo = txnNo,
                TxnType = Cat.TxnIssue,
                TxnDate = date,
                LotId = line.LotId,
                QuantityKg = line.Kg,
                FromStage = Cat.Activated,
                FromCompartmentId = bin,
                WelderId = welder.Id,
                Requestor = welder.WelderName,
                Remarks = T.FreeText(r.Remarks, 500),
                CreatedBy = user,
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        List<ResidualLot> residuals = r.TakeAll ? [] : await ResidualsAsync(item, bin, binLots, lines, ct);
        return Ok(await ResultAsync(txnNo, item.Id, residuals, null, ct));
    }

    public async Task<ServiceResult<MovementResult>> ReturnAsync(ReturnRequest r, string? enteredBy, bool supervisor, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);

        var qty = T.RoundKg(r.QuantityKg);
        if (qty <= 0) return Fail("Return quantity must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var (welder, welderError) = await guards.StockWelderAsync(r.WelderId, ct);
        if (welder is null) return Fail(welderError!);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);

        int lotId;
        if (r.LotId is int requested)
        {
            if (!await db.ConsumableItemLots.AnyAsync(l => l.Id == requested && l.ItemId == item.Id, ct))
                return Fail("The selected lot does not belong to this consumable.");
            lotId = requested;
        }
        else
        {
            var fallback = await DefaultReturnLotAsync(welder.Id, item.Id, ct);
            if (fallback is null) return Fail("This consumable has no lots yet, so nothing can be returned.");
            lotId = fallback.Value;
        }

        var movement = new ConsumableMovement
        {
            TxnType = Cat.TxnReturn,
            TxnDate = date,
            LotId = lotId,
            QuantityKg = qty,
            WelderId = welder.Id,
            Requestor = welder.WelderName,
            Remarks = T.FreeText(r.Remarks, 500),
            CreatedBy = user,
        };

        if (r.ForRebake)
        {
            if (!item.IsElectrode) return Fail("Only electrodes can be returned for re-baking.");
            var (record, rebakeError) = await RebakeRecordAsync(lotId, r.BakingRecordId, ct);
            if (record is null) return Fail(rebakeError!, StatusCodes.Status409Conflict);
            movement.ToStage = Cat.Baking;
            movement.BakingRecordId = record.Id;
        }
        else
        {
            var (bin, binError) = G.ResolveBin(item, r.CompartmentId);
            if (binError is not null) return Fail(binError);
            if (bin is int compartmentId)
            {
                var compartmentError = await guards.CheckCompartmentAsync(item, compartmentId, ct);
                if (compartmentError is not null) return Fail(compartmentError, StatusCodes.Status409Conflict);
            }
            movement.ToStage = Cat.Activated;
            movement.ToCompartmentId = bin;
        }

        var since = date.AddDays(-(Math.Max(settings.ReturnWindowDays, 1) - 1));
        var outstanding = await guards.OutstandingAsync(welder.Id, item.Id, since, date, ct);
        var overReturn = qty > outstanding;
        if (overReturn && !supervisor)
            return Fail($"{welder.WelderName} has only {Math.Max(outstanding, 0m):0.00} kg of {item.DiaSpec} to return from the last " +
                        $"{settings.ReturnWindowDays} day(s). Ask a supervisor to record a larger return.", StatusCodes.Status409Conflict);
        if (r.ForRebake && movement.BakingRecordId is int rebakeId)
        {
            var issuedFromRecord = await ledger.Live()
                .Where(m => m.BakingRecordId == rebakeId && m.FromStage == Cat.Baking && m.ToStage == Cat.Activated)
                .SumAsync(m => (decimal?)m.QuantityKg, ct) ?? 0m;
            if (qty > issuedFromRecord)
                return Fail($"Only {issuedFromRecord:0.00} kg came out of this baking record, so no more than that can be re-baked.",
                    StatusCodes.Status409Conflict);
        }
        var warning = overReturn
            ? $"{welder.WelderName} has only {Math.Max(outstanding, 0m):0.00} kg of {item.DiaSpec} outstanding from the last " +
              $"{settings.ReturnWindowDays} day(s). The return was recorded as entered by the supervisor."
            : null;

        var txnNo = await ledger.NextTxnNoAsync(ct);
        movement.TxnNo = txnNo;
        db.ConsumableMovements.Add(movement);
        await db.SaveChangesAsync(ct);

        if (movement.BakingRecordId is int bakingId)
        {
            await ledger.RefreshBakingStatusAsync([bakingId], ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], warning, ct));
    }

    public async Task<ServiceResult<MovementResult>> MoveAsync(MoveRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);
        if (r.FromCompartmentId == r.ToCompartmentId) return Fail("Choose a different compartment to move to.");

        var qty = T.RoundKg(r.QuantityKg);
        if (!r.TakeAll && qty <= 0) return Fail("Quantity must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);
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
        foreach (var line in lines)
        {
            db.ConsumableMovements.Add(new ConsumableMovement
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
            });
        }
        await db.SaveChangesAsync(ct);

        if (r.FromCompartmentId is int fromId)
        {
            await RelocateHoldingsAsync(item.Id, lines.Select(l => l.LotId), fromId, r.ToCompartmentId, ct);
            await db.SaveChangesAsync(ct);
        }
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

        var holdings = await db.HoldingRecords
            .Where(h => !h.IsVoided && !h.IsFinishedAfterBaking && h.CompartmentId == fromId && emptied.Contains(h.BakingRecord.LotId))
            .ToListAsync(ct);
        foreach (var holding in holdings) holding.CompartmentId = toId;
    }

    public async Task<ServiceResult<MovementResult>> FinishAsync(FinishRequest r, string? enteredBy, bool supervisor, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, null);
        if (error is not null) return Fail(error);

        var stage = r.Stage is null ? Cat.Activated : T.Stage(r.Stage);
        if (stage != Cat.Activated) return Fail("Only Activated storage can be marked as finished.");

        var reason = r.Reason is null ? Cat.ReasonUsedUp : T.Reason(r.Reason);
        if (reason is null) return Fail($"Reason must be one of: {string.Join(", ", Cat.AdjustReasons)}.");
        if (!supervisor && (r.LotId is null || reason != Cat.ReasonUsedUp))
            return Fail("Welders can only mark a single lot's leftover as used up. Ask a supervisor for other write-offs.",
                StatusCodes.Status403Forbidden);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);

        var (bin, binError) = G.ResolveBin(item, r.CompartmentId);
        if (binError is not null) return Fail(binError);

        var lines = (await guards.BinLotsAsync(item.Id, bin, ct))
            .Where(l => (r.LotId is null || l.LotId == r.LotId) && l.Available > 0)
            .OrderBy(l => l.LotId)
            .Select(l => new StockLine(l.LotId, l.Available))
            .ToList();
        if (lines.Count == 0)
            return Fail($"There is nothing left in {await guards.BinNameAsync(bin, ct)} to mark as finished.", StatusCodes.Status409Conflict);
        if (!supervisor)
        {
            var threshold = item.FinishThresholdKg ?? settings.FinishThresholdKg;
            var leftover = lines.Sum(l => l.Kg);
            if (leftover > threshold)
                return Fail($"{leftover:0.00} kg is more than the {threshold:0.00} kg leftover a welder can mark as used up. " +
                            "Ask a supervisor to record it.", StatusCodes.Status403Forbidden);
        }

        var txnNo = await ledger.NextTxnNoAsync(ct);
        foreach (var line in lines)
        {
            db.ConsumableMovements.Add(new ConsumableMovement
            {
                TxnNo = txnNo,
                TxnType = Cat.TxnFinish,
                TxnDate = date,
                LotId = line.LotId,
                QuantityKg = line.Kg,
                FromStage = Cat.Activated,
                FromCompartmentId = bin,
                Reason = reason,
                Remarks = T.FreeText(r.Remarks, 500),
                CreatedBy = user,
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    public async Task<ServiceResult<MovementResult>> AdjustAsync(AdjustRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);

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
        if (item is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);

        if (r.LotId is int requested && !await db.ConsumableItemLots.AnyAsync(l => l.Id == requested && l.ItemId == item.Id, ct))
            return Fail("The selected lot does not belong to this consumable.");

        int? bin = null;
        if (stage == Cat.Activated)
        {
            var (resolved, binError) = G.ResolveBin(item, r.CompartmentId);
            if (binError is not null) return Fail(binError);
            bin = resolved;
        }
        else if (r.CompartmentId is not null)
        {
            return Fail("Compartments apply to Activated storage only.");
        }

        var available = stage == Cat.Normal
            ? (await ledger.LotStagesForItemAsync(item.Id, ct)).Select(l => (LotId: l.LotId, Available: l.Totals.NormalKg)).ToList()
            : await guards.BinLotsAsync(item.Id, bin, ct);
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
                ?? await db.ConsumableItemLots.Where(l => l.ItemId == item.Id)
                    .OrderByDescending(l => l.Id).Select(l => (int?)l.Id).FirstOrDefaultAsync(ct);
            if (target is null) return Fail("Receive this consumable at least once before adjusting it.");
            lines = [new StockLine(target.Value, difference)];
        }

        var txnNo = await ledger.NextTxnNoAsync(ct);
        foreach (var line in lines)
        {
            db.ConsumableMovements.Add(new ConsumableMovement
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
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    public async Task<ServiceResult<MovementResult>> VoidAsync(string txnNo, VoidRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, _, error) = G.Common(enteredBy, null);
        if (error is not null) return Fail(error);

        var remarks = T.FreeText(r.Remarks, 500);
        if (remarks is null) return Fail("Remarks are required to void a transaction.");

        var heads = await db.ConsumableMovements.AsNoTracking()
            .Where(m => m.TxnNo == txnNo)
            .Select(m => new { m.TxnType, m.IsVoided, m.Lot.ItemId })
            .ToListAsync(ct);
        if (heads.Count == 0) return Fail("Transaction not found.", StatusCodes.Status404NotFound);
        if (heads.Any(h => h.TxnType == Cat.TxnVoid)) return Fail("A void entry cannot itself be voided.");
        if (heads.All(h => h.IsVoided)) return Fail("This transaction has already been voided.", StatusCodes.Status409Conflict);

        var itemIds = heads.Select(h => h.ItemId).Distinct().Order().ToList();

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var itemId in itemIds) await StockLocks.AcquireAsync(db, StockLocks.Item(itemId), ct);

        var originals = await db.ConsumableMovements
            .Include(m => m.Lot)
            .Where(m => m.TxnNo == txnNo && !m.IsVoided)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);
        if (originals.Count == 0) return Fail("This transaction has already been voided.", StatusCodes.Status409Conflict);

        var stages = (await ledger.LotStagesAsync(m => itemIds.Contains(m.Lot.ItemId), ct))
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
            if (restoreItem is null) return Fail("Consumable not found.", StatusCodes.Status404NotFound);
            var compartmentError = await guards.CheckCompartmentAsync(restoreItem, restore.CompartmentId, ct);
            if (compartmentError is not null)
                return Fail($"Voiding {txnNo} would put stock back where it no longer fits: {compartmentError}", StatusCodes.Status409Conflict);
        }

        var bins = await ledger.ActivatedBinsAsync(m => itemIds.Contains(m.Lot.ItemId), ct);
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
        foreach (var original in originals)
        {
            original.IsVoided = true;
            db.ConsumableMovements.Add(new ConsumableMovement
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
            });
        }

        var holdings = await db.HoldingRecords.Where(h => h.TxnNo == txnNo && !h.IsVoided).ToListAsync(ct);
        foreach (var holding in holdings) holding.IsVoided = true;

        var rebakeIds = originals
            .Where(o => o.TxnType == Cat.TxnReturn && o.ToStage == Cat.Baking && o.BakingRecordId is not null)
            .Select(o => o.BakingRecordId!.Value)
            .Distinct()
            .ToList();
        if (rebakeIds.Count > 0)
        {
            foreach (var record in await db.BakingRecords.Where(b => rebakeIds.Contains(b.Id)).ToListAsync(ct))
            {
                record.RebakeStart = null;
                record.RebakeStop = null;
            }
        }

        await db.SaveChangesAsync(ct);

        foreach (var moved in originals
                     .Where(o => o.TxnType == Cat.TxnMove && o.FromCompartmentId is not null && o.ToCompartmentId is not null)
                     .GroupBy(o => new { o.Lot.ItemId, From = o.FromCompartmentId!.Value, To = o.ToCompartmentId!.Value }))
        {
            await RelocateHoldingsAsync(moved.Key.ItemId, moved.Select(o => o.LotId), moved.Key.To, moved.Key.From, ct);
        }
        await db.SaveChangesAsync(ct);

        var bakingIds = originals.Where(o => o.BakingRecordId is not null).Select(o => o.BakingRecordId!.Value).ToList();
        if (bakingIds.Count > 0)
        {
            await ledger.RefreshBakingStatusAsync(bakingIds, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(voidNo, itemIds[0], [], null, ct));
    }

    private async Task<(BakingRecord? Record, string? Error)> RebakeRecordAsync(int lotId, int? requestedId, CancellationToken ct)
    {
        var record = requestedId is int id
            ? await db.BakingRecords.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == id && b.LotId == lotId && b.Status != Cat.StatusCancelled, ct)
            : await db.BakingRecords.AsNoTracking()
                .Where(b => b.LotId == lotId && b.BakeStop != null && b.Status != Cat.StatusCancelled)
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync(ct);
        if (record is null) return (null, "No completed baking record was found for this lot, so it cannot be re-baked.");
        if (record.BakeStop is null) return (null, $"{record.BakingNo} has not finished its first bake yet.");

        var alreadyReturned = await ledger.Live().AnyAsync(m =>
            m.BakingRecordId == record.Id && m.TxnType == Cat.TxnReturn && m.ToStage == Cat.Baking, ct);
        if (record.RebakeStart is not null || alreadyReturned)
            return (null, $"{record.BakingNo} has already been re-baked once. These electrodes cannot be re-baked again; " +
                          "scrap them outside the system (they are already counted as used).");

        var balance = (await ledger.BakingBalancesAsync([record.Id], ct)).GetValueOrDefault(record.Id);
        if (balance > 0)
            return (null, $"{record.BakingNo} still has {balance:0.00} kg baked and not yet placed. " +
                          "Place or finish it before returning electrodes for re-baking.");

        return (record, null);
    }

    private async Task<List<ResidualLot>> ResidualsAsync(
        ItemRef item, int? bin, List<(int LotId, decimal Available)> before, List<StockLine> taken, CancellationToken ct)
    {
        var threshold = item.FinishThresholdKg ?? settings.FinishThresholdKg;
        if (threshold <= 0) return [];

        var remaining = before
            .GroupBy(l => l.LotId)
            .Select(g => new { LotId = g.Key, Kg = g.Sum(x => x.Available) - taken.Where(t => t.LotId == g.Key).Sum(t => t.Kg) })
            .Where(l => l.Kg > 0)
            .ToList();
        var binRemaining = remaining.Sum(l => l.Kg);
        var touched = taken.Select(t => t.LotId).ToHashSet();

        var candidates = binRemaining <= threshold
            ? remaining
            : remaining.Where(l => touched.Contains(l.LotId) && l.Kg <= threshold).ToList();
        if (candidates.Count == 0) return [];

        var ids = candidates.Select(c => c.LotId).ToList();
        var meta = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.Brand, l.LotNumber })
            .ToDictionaryAsync(l => l.Id, ct);
        string? label = bin is int id ? (await ledger.CompartmentLabelsAsync([id], ct)).GetValueOrDefault(id) : null;

        return candidates
            .Select(c => new ResidualLot(item.Id, c.LotId, item.DiaSpec, meta[c.LotId].Brand, meta[c.LotId].LotNumber,
                Cat.Activated, c.Kg, bin, label))
            .ToList();
    }

    private async Task<int?> DefaultReturnLotAsync(int welderId, int itemId, CancellationToken ct)
    {
        var lastIssued = await ledger.Live()
            .Where(m => m.TxnType == Cat.TxnIssue && m.WelderId == welderId && m.Lot.ItemId == itemId)
            .OrderByDescending(m => m.Id)
            .Select(m => (int?)m.LotId)
            .FirstOrDefaultAsync(ct);
        if (lastIssued is not null) return lastIssued;

        var activated = (await ledger.LotStagesForItemAsync(itemId, ct))
            .Where(l => l.Totals.ActivatedKg > 0)
            .OrderByDescending(l => l.LotId)
            .Select(l => (int?)l.LotId)
            .FirstOrDefault();
        if (activated is not null) return activated;

        return await db.ConsumableItemLots.AsNoTracking()
            .Where(l => l.ItemId == itemId)
            .OrderByDescending(l => l.Id)
            .Select(l => (int?)l.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<MovementResult> ResultAsync(
        string txnNo, int itemId, List<ResidualLot> residuals, string? warning, CancellationToken ct)
    {
        var lines = await ConsumableLedger.Project(db.ConsumableMovements.AsNoTracking()
                .Where(m => m.TxnNo == txnNo)
                .OrderBy(m => m.Id))
            .ToListAsync(ct);
        return new MovementResult(txnNo, lines, await ledger.StageBalanceAsync(itemId, ct), residuals, warning);
    }

    private static ServiceResult<MovementResult> Ok(MovementResult value) => ServiceResult<MovementResult>.Ok(value);

    private static ServiceResult<MovementResult> Fail(string error, int status = StatusCodes.Status400BadRequest)
        => ServiceResult<MovementResult>.Fail(error, status);
}
