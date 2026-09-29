using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

// Counter commands: welders issue, return and finish electrodes.
public partial class ConsumableMovementService
{
    public async Task<ServiceResult<MovementResult>> IssueAsync(IssueRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = guards.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);
        if (guards.CheckBackdate(date) is string backdate) return Fail(backdate);

        var qty = T.RoundKg(r.QuantityKg);
        if (!r.TakeAll && qty <= 0) return Fail("Take/KG must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var (welder, welderError) = await guards.StockWelderAsync(r.WelderId, ct);
        if (welder is null) return Fail(welderError!);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return ItemNotFound();

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
        await store.AddMovementsAsync(lines.Select(line => new ConsumableMovement
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
        }), ct);
        await tx.CommitAsync(ct);

        List<ResidualLot> residuals = r.TakeAll ? [] : await ResidualsAsync(item, bin, binLots, lines, ct);
        return Ok(await ResultAsync(txnNo, item.Id, residuals, null, ct));
    }

    public async Task<ServiceResult<MovementResult>> ReturnAsync(ReturnRequest r, string? enteredBy, bool supervisor, CancellationToken ct)
    {
        var (user, date, error) = guards.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);
        if (guards.CheckBackdate(date) is string backdate) return Fail(backdate);

        var qty = T.RoundKg(r.QuantityKg);
        if (qty <= 0) return Fail("Return quantity must be greater than 0.");
        if (qty > T.MaxKg) return Fail(T.MaxKgError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var (welder, welderError) = await guards.StockWelderAsync(r.WelderId, ct);
        if (welder is null) return Fail(welderError!);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return ItemNotFound();

        int lotId;
        if (r.LotId is int requested)
        {
            if (!(await store.LotsAsync([requested], null, null, ct)).Any(l => l.ItemId == item.Id))
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
            var issuedFromRecord = (await ledger.BakingFlagsAsync(rebakeId, ct)).IssuedToActivatedKg ?? 0m;
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
        await store.AddMovementsAsync([movement], ct);

        if (movement.BakingRecordId is int bakingId)
            await ledger.RefreshBakingStatusAsync([bakingId], ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], warning, ct));
    }

    public async Task<ServiceResult<MovementResult>> FinishAsync(FinishRequest r, string? enteredBy, bool supervisor, CancellationToken ct)
    {
        var (user, date, error) = guards.Common(enteredBy, null);
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
        if (item is null) return ItemNotFound();

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
        await store.AddMovementsAsync(lines.Select(line => new ConsumableMovement
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
        }), ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    private async Task<(BakingRecord? Record, string? Error)> RebakeRecordAsync(int lotId, int? requestedId, CancellationToken ct)
    {
        var record = await store.RebakeRecordAsync(lotId, requestedId, ct);
        if (record is null) return (null, "No completed baking record was found for this lot, so it cannot be re-baked.");
        if (record.BakeStop is null) return (null, $"{record.BakingNo} has not finished its first bake yet.");

        var alreadyReturned = (await ledger.BakingFlagsAsync(record.Id, ct)).RebakeReturned;
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
        var meta = (await store.LotsAsync(ids, null, null, ct)).ToDictionary(l => l.Id);
        string? label = bin is int id ? (await ledger.CompartmentLabelsAsync([id], ct)).GetValueOrDefault(id) : null;

        return candidates
            .Select(c => new ResidualLot(item.Id, c.LotId, item.DiaSpec, meta[c.LotId].Brand, meta[c.LotId].LotNumber,
                Cat.Activated, c.Kg, bin, label))
            .ToList();
    }

    private async Task<int?> DefaultReturnLotAsync(int welderId, int itemId, CancellationToken ct)
    {
        var lastIssued = await sp.FirstOrDefaultAsync<int?>("SP_DCenter_Ledger_LastIssuedLot", ct,
            Sql.Int("@WelderId", welderId), Sql.Int("@ItemId", itemId));
        if (lastIssued is not null) return lastIssued;

        var activated = (await ledger.LotStagesForItemAsync(itemId, ct))
            .Where(l => l.Totals.ActivatedKg > 0)
            .OrderByDescending(l => l.LotId)
            .Select(l => (int?)l.LotId)
            .FirstOrDefault();
        if (activated is not null) return activated;

        return (await store.LotsAsync(null, [itemId], null, ct)).Select(l => (int?)l.Id).DefaultIfEmpty().Max();
    }
}
