using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public class BakingService(WeldReportContext db, ConsumableLedger ledger, ConsumableItemService items, ConsumableGuards guards)
{
    private static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    public async Task<ServiceResult<BakingResult>> SendToBakeAsync(SendToBakeRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.BakingDate);
        if (error is not null) return Fail<BakingResult>(error);

        var pic = T.FreeText(r.PersonInCharge, 100);
        if (pic is null) return Fail<BakingResult>("Person In Charge is required.");

        var qty = T.RoundKg(r.QuantityKg);
        if (qty <= 0) return Fail<BakingResult>("Quantity must be greater than 0.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(r.ItemId), ct);

        var item = await guards.ItemAsync(r.ItemId, ct);
        if (item is null) return Fail<BakingResult>("Consumable not found.", StatusCodes.Status404NotFound);
        if (!item.IsElectrode) return Fail<BakingResult>("Only electrodes are sent for baking.");

        var normal = (await ledger.LotStagesForItemAsync(item.Id, ct))
            .Select(l => (LotId: l.LotId, Available: l.Totals.NormalKg))
            .ToList();
        var (lines, takeError) = G.Take(normal, r.LotId, qty, "Normal storage");
        if (lines is null) return Fail<BakingResult>(takeError!, StatusCodes.Status409Conflict);

        await items.EnsureLookupsAsync([(Cat.PersonInChargeLookup, pic)], ct);

        var txnNo = await ledger.NextTxnNoAsync(ct);
        var records = new List<BakingRecord>();
        foreach (var line in lines)
        {
            var record = new BakingRecord
            {
                BakingNo = await ledger.NextBakingNoAsync(ct),
                LotId = line.LotId,
                QuantityKg = line.Kg,
                PersonInCharge = pic,
                BakingDate = date,
                Status = Cat.StatusQueued,
                Remarks = T.FreeText(r.Remarks, 500),
                CreatedBy = user,
            };
            records.Add(record);
            db.BakingRecords.Add(record);
            db.ConsumableMovements.Add(new ConsumableMovement
            {
                TxnNo = txnNo,
                TxnType = Cat.TxnSendToBake,
                TxnDate = date,
                LotId = line.LotId,
                QuantityKg = line.Kg,
                FromStage = Cat.Normal,
                ToStage = Cat.Baking,
                BakingRecord = record,
                Remarks = T.FreeText(r.Remarks, 500),
                CreatedBy = user,
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<BakingResult>.Ok(new BakingResult(txnNo, await RecordsAsync(records.Select(x => x.Id).ToList(), ct)));
    }

    public async Task<ServiceResult<BakingResult>> StartAsync(BakingTimesRequest r, string? enteredBy, CancellationToken ct)
        => await StampAsync(r, enteredBy, start: true, ct);

    public async Task<ServiceResult<BakingResult>> StopAsync(BakingTimesRequest r, string? enteredBy, CancellationToken ct)
        => await StampAsync(r, enteredBy, start: false, ct);

    public async Task<ServiceResult<BakingRecordDto>> UpdateAsync(int id, BakingUpdate u, string? enteredBy, CancellationToken ct)
    {
        var (_, _, error) = G.Common(enteredBy, u.BakingDate);
        if (error is not null) return Fail<BakingRecordDto>(error);

        var pic = T.FreeText(u.PersonInCharge, 100);
        if (pic is null) return Fail<BakingRecordDto>("Person In Charge is required.");

        var timeError = CheckTimes(u.BakeStart, u.BakeStop, u.RebakeStart, u.RebakeStop);
        if (timeError is not null) return Fail<BakingRecordDto>(timeError);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var record = await db.BakingRecords.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (record is null) return Fail<BakingRecordDto>("Baking record not found.", StatusCodes.Status404NotFound);
        if (record.Status == Cat.StatusCancelled) return Fail<BakingRecordDto>("This baking record was cancelled.");

        var rebakeReturned = await ledger.Live().AnyAsync(m =>
            m.BakingRecordId == id && m.TxnType == Cat.TxnReturn && m.ToStage == Cat.Baking, ct);
        if (!rebakeReturned && (u.RebakeStart is not null || u.RebakeStop is not null) && record.RebakeStart is null)
            return Fail<BakingRecordDto>("Re-bake times can only be entered after electrodes are returned for re-baking.");

        var placed = await ledger.Live().AnyAsync(m => m.BakingRecordId == id && m.FromStage == Cat.Baking, ct);
        if (placed && (u.BakeStart is null || u.BakeStop is null))
            return Fail<BakingRecordDto>("Start and stop times are required once electrodes from this batch have been placed or issued.");

        record.PersonInCharge = pic;
        record.BakingDate = u.BakingDate ?? record.BakingDate;
        record.BakeStart = u.BakeStart;
        record.BakeStop = u.BakeStop;
        record.RebakeStart = u.RebakeStart;
        record.RebakeStop = u.RebakeStop;
        record.Remarks = T.FreeText(u.Remarks, 500);
        await items.EnsureLookupsAsync([(Cat.PersonInChargeLookup, pic)], ct);
        await db.SaveChangesAsync(ct);
        await ledger.RefreshBakingStatusAsync([id], ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<BakingRecordDto>.Ok((await RecordsAsync([id], ct))[0]);
    }

    public async Task<ServiceResult<PlaceResult>> PlaceAsync(PlaceRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = G.Common(enteredBy, r.HoldingDate);
        if (error is not null) return Fail<PlaceResult>(error);

        var qty = T.RoundKg(r.QuantityKg);
        if (!r.TakeAll && qty <= 0) return Fail<PlaceResult>("Quantity must be greater than 0.");
        if (r.FinishedAfterBaking && r.WelderId is null) return Fail<PlaceResult>("Welder Name is required for Finished After Baking.");
        if (!r.FinishedAfterBaking && r.CompartmentId is null) return Fail<PlaceResult>("Choose a compartment, or select Finished After Baking.");

        var head = await db.BakingRecords.AsNoTracking()
            .Where(b => b.Id == r.BakingRecordId)
            .Select(b => new { b.Id, b.BakingNo, b.LotId, b.Lot.ItemId })
            .FirstOrDefaultAsync(ct);
        if (head is null) return Fail<PlaceResult>("Baking record not found.", StatusCodes.Status404NotFound);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Item(head.ItemId), ct);

        var record = await db.BakingRecords.FirstAsync(b => b.Id == head.Id, ct);
        if (record.Status is not (Cat.StatusBaked or Cat.StatusRebaked))
            return Fail<PlaceResult>($"{record.BakingNo} is {StatusText(record.Status)}. Record the stop time before placing it.",
                StatusCodes.Status409Conflict);

        var item = (await guards.ItemAsync(head.ItemId, ct))!;
        var balance = (await ledger.BakingBalancesAsync([record.Id], ct)).GetValueOrDefault(record.Id);
        if (r.TakeAll) qty = balance;
        if (qty <= 0 || qty > balance)
            return Fail<PlaceResult>($"Only {balance:0.00} kg of {record.BakingNo} is waiting to be placed.", StatusCodes.Status409Conflict);

        WelderRef? welder = null;
        if (r.WelderId is int welderId)
        {
            var (found, welderError) = await guards.StockWelderAsync(welderId, ct);
            if (found is null) return Fail<PlaceResult>(welderError!);
            welder = found;
        }

        string? warning = null;
        if (!r.FinishedAfterBaking)
        {
            var compartmentError = await guards.CheckCompartmentAsync(item, r.CompartmentId!.Value, ct);
            if (compartmentError is not null) return Fail<PlaceResult>(compartmentError, StatusCodes.Status409Conflict);
            warning = await guards.SameItemOtherLotWarningAsync(item.Id, record.LotId, r.CompartmentId.Value, ct);
        }

        var txnNo = await ledger.NextTxnNoAsync(ct);
        db.ConsumableMovements.Add(new ConsumableMovement
        {
            TxnNo = txnNo,
            TxnType = r.FinishedAfterBaking ? Cat.TxnIssue : Cat.TxnHold,
            TxnDate = date,
            LotId = record.LotId,
            QuantityKg = qty,
            FromStage = Cat.Baking,
            ToStage = r.FinishedAfterBaking ? null : Cat.Activated,
            ToCompartmentId = r.FinishedAfterBaking ? null : r.CompartmentId,
            BakingRecordId = record.Id,
            WelderId = welder?.Id,
            Requestor = welder?.WelderName,
            Remarks = T.FreeText(r.Remarks, 500),
            CreatedBy = user,
        });

        var holding = new HoldingRecord
        {
            HoldingNo = await ledger.NextHoldingNoAsync(ct),
            HoldingDate = date,
            BakingRecordId = record.Id,
            WelderId = welder?.Id,
            WelderName = welder?.WelderName,
            CompartmentId = r.FinishedAfterBaking ? null : r.CompartmentId,
            IsFinishedAfterBaking = r.FinishedAfterBaking,
            QuantityKg = qty,
            TxnNo = txnNo,
            Remarks = T.FreeText(r.Remarks, 500),
            CreatedBy = user,
        };
        db.HoldingRecords.Add(holding);
        await db.SaveChangesAsync(ct);
        await ledger.RefreshBakingStatusAsync([record.Id], ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var holdingDto = (await HoldingsAsync(db.HoldingRecords.AsNoTracking().Where(h => h.Id == holding.Id), ct))[0];
        return ServiceResult<PlaceResult>.Ok(new PlaceResult(holdingDto, (await RecordsAsync([record.Id], ct))[0], warning));
    }

    public async Task<List<BakingRecordDto>> GetBoardAsync(CancellationToken ct)
    {
        var open = Cat.OpenBakingStatuses.ToList();
        var ids = await db.BakingRecords.AsNoTracking()
            .Where(b => open.Contains(b.Status))
            .Select(b => b.Id)
            .ToListAsync(ct);
        return await RecordsAsync(ids, ct);
    }

    public async Task<ServiceResult<BakingPage>> GetRecordsAsync(BakingQuery p, CancellationToken ct)
    {
        string? status = null;
        if (p.Status is not null)
        {
            status = Cat.BakingStatuses.FirstOrDefault(s => string.Equals(s, p.Status, StringComparison.OrdinalIgnoreCase));
            if (status is null) return Fail<BakingPage>("Unknown baking status.");
        }

        var q = db.BakingRecords.AsNoTracking();
        if (p.From is DateOnly from) q = q.Where(b => b.BakingDate >= from);
        if (p.To is DateOnly to) q = q.Where(b => b.BakingDate <= to);
        if (status is not null) q = q.Where(b => b.Status == status);
        if (p.OpenOnly)
        {
            var open = Cat.OpenBakingStatuses.ToList();
            q = q.Where(b => open.Contains(b.Status));
        }
        foreach (var term in T.Terms(p.Q))
        {
            q = q.Where(b => b.BakingNo.Contains(term) || b.PersonInCharge.Contains(term) || b.Lot.LotNumber.Contains(term)
                             || b.Lot.Brand.Contains(term) || b.Lot.Item.Specification.Contains(term)
                             || b.Lot.Item.Diameter.Contains(term));
        }

        var total = await q.CountAsync(ct);
        var ids = await q.OrderByDescending(b => b.Id)
            .Skip(Math.Max(p.Skip, 0))
            .Take(Math.Clamp(p.Take, 1, 200))
            .Select(b => b.Id)
            .ToListAsync(ct);
        var rows = await RecordsAsync(ids, ct);
        return ServiceResult<BakingPage>.Ok(new BakingPage(rows.OrderByDescending(r => r.Id).ToList(), total));
    }

    public async Task<HoldingPage> GetHoldingsAsync(HoldingQuery p, CancellationToken ct)
    {
        var q = db.HoldingRecords.AsNoTracking();
        if (p.From is DateOnly from) q = q.Where(h => h.HoldingDate >= from);
        if (p.To is DateOnly to) q = q.Where(h => h.HoldingDate <= to);
        foreach (var term in T.Terms(p.Q))
        {
            q = q.Where(h => h.HoldingNo.Contains(term) || h.BakingRecord.BakingNo.Contains(term)
                             || h.BakingRecord.Lot.LotNumber.Contains(term) || h.BakingRecord.Lot.Item.Specification.Contains(term)
                             || (h.WelderName != null && h.WelderName.Contains(term)));
        }

        var total = await q.CountAsync(ct);
        var rows = await HoldingsAsync(q.OrderByDescending(h => h.Id).Skip(Math.Max(p.Skip, 0)).Take(Math.Clamp(p.Take, 1, 200)), ct);
        return new HoldingPage(rows, total);
    }

    private async Task<ServiceResult<BakingResult>> StampAsync(BakingTimesRequest r, string? enteredBy, bool start, CancellationToken ct)
    {
        var (_, _, error) = G.Common(enteredBy, null);
        if (error is not null) return Fail<BakingResult>(error);

        var ids = (r.Ids ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0) return Fail<BakingResult>("Select at least one baking record.");

        var at = r.At ?? DateTime.Now;
        if (at > DateTime.Now + ClockTolerance) return Fail<BakingResult>("The time cannot be in the future.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var records = await db.BakingRecords.Where(b => ids.Contains(b.Id)).ToListAsync(ct);
        if (records.Count != ids.Count) return Fail<BakingResult>("One or more baking records were not found.", StatusCodes.Status404NotFound);

        foreach (var record in records)
        {
            switch (start, record.Status)
            {
                case (true, Cat.StatusQueued):
                    record.BakeStart = at;
                    break;
                case (true, Cat.StatusRebakeQueued):
                    if (record.BakeStop is DateTime stop && at <= stop)
                        return Fail<BakingResult>($"{record.BakingNo}: re-bake start must be after the first bake stopped.");
                    record.RebakeStart = at;
                    break;
                case (false, Cat.StatusBaking):
                    if (record.BakeStart is DateTime bakeStart && at <= bakeStart)
                        return Fail<BakingResult>($"{record.BakingNo}: stop time must be after the start time.");
                    record.BakeStop = at;
                    break;
                case (false, Cat.StatusRebaking):
                    if (record.RebakeStart is DateTime rebakeStart && at <= rebakeStart)
                        return Fail<BakingResult>($"{record.BakingNo}: stop time must be after the re-bake start time.");
                    record.RebakeStop = at;
                    break;
                default:
                    return Fail<BakingResult>(
                        $"{record.BakingNo} is {StatusText(record.Status)} and cannot be {(start ? "started" : "stopped")}.",
                        StatusCodes.Status409Conflict);
            }
        }

        await db.SaveChangesAsync(ct);
        await ledger.RefreshBakingStatusAsync(ids, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ServiceResult<BakingResult>.Ok(new BakingResult(null, await RecordsAsync(ids, ct)));
    }

    private static string? CheckTimes(DateTime? bakeStart, DateTime? bakeStop, DateTime? rebakeStart, DateTime? rebakeStop)
    {
        var limit = DateTime.Now + ClockTolerance;
        if (new[] { bakeStart, bakeStop, rebakeStart, rebakeStop }.Any(t => t > limit)) return "Times cannot be in the future.";
        if (bakeStop is not null && (bakeStart is null || bakeStop <= bakeStart)) return "Bake stop must be after bake start.";
        if (rebakeStart is not null && (bakeStop is null || rebakeStart <= bakeStop)) return "Re-bake start must be after the first bake stopped.";
        if (rebakeStop is not null && (rebakeStart is null || rebakeStop <= rebakeStart)) return "Re-bake stop must be after re-bake start.";
        return null;
    }

    private static string StatusText(string status) => status switch
    {
        Cat.StatusRebakeQueued => "queued for re-baking",
        _ => status.ToLowerInvariant(),
    };

    private async Task<List<BakingRecordDto>> RecordsAsync(List<int> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var rows = await db.BakingRecords.AsNoTracking()
            .Where(b => ids.Contains(b.Id))
            .Select(b => new
            {
                b.Id, b.BakingNo, b.Lot.ItemId, b.Lot.Item.Category, b.Lot.Item.Diameter, b.Lot.Item.Specification,
                b.Lot.Item.HoldingOvenType, b.LotId, b.Lot.Brand, b.Lot.LotNumber, b.QuantityKg, b.PersonInCharge, b.BakingDate,
                b.BakeStart, b.BakeStop, b.RebakeStart, b.RebakeStop, b.Status, b.Remarks, b.CreatedBy, b.CreatedAt,
            })
            .ToListAsync(ct);
        var balances = await ledger.BakingBalancesAsync(ids, ct);
        return rows
            .OrderBy(b => b.Id)
            .Select(b => new BakingRecordDto(b.Id, b.BakingNo, b.ItemId, b.Category, Cat.DiaSpec(b.Diameter, b.Specification),
                b.HoldingOvenType, b.LotId, b.Brand, b.LotNumber, b.QuantityKg, balances.GetValueOrDefault(b.Id),
                b.PersonInCharge, b.BakingDate, b.BakeStart, b.BakeStop, b.RebakeStart, b.RebakeStop, b.Status, b.Remarks,
                b.CreatedBy, b.CreatedAt))
            .ToList();
    }

    private static Task<List<HoldingRecordDto>> HoldingsAsync(IQueryable<HoldingRecord> q, CancellationToken ct)
        => q.Select(h => new HoldingRecordDto(
                h.Id, h.HoldingNo, h.HoldingDate, h.BakingRecordId, h.BakingRecord.BakingNo, h.BakingRecord.Lot.ItemId,
                h.BakingRecord.Lot.Item.Diameter + " " + h.BakingRecord.Lot.Item.Specification,
                h.BakingRecord.LotId, h.BakingRecord.Lot.Brand, h.BakingRecord.Lot.LotNumber, h.WelderId, h.WelderName,
                h.CompartmentId, h.Compartment != null ? h.Compartment.Oven.Code + "-" + h.Compartment.Label : null,
                h.Compartment != null ? h.Compartment.Oven.OvenType : null, h.Compartment != null ? h.Compartment.Number : (int?)null,
                h.IsFinishedAfterBaking, h.QuantityKg, h.TxnNo, h.IsVoided, h.Remarks, h.CreatedBy, h.CreatedAt))
            .ToListAsync(ct);

    private static ServiceResult<TResult> Fail<TResult>(string error, int status = StatusCodes.Status400BadRequest)
        => ServiceResult<TResult>.Fail(error, status);
}
