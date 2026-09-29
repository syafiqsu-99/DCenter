using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Extensions.Options;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public partial class ConsumableMovementService(
    WeldReportContext db, StoredProcedures sp, ConsumableStore store, ConsumableLedger ledger, ConsumableItemService items,
    ConsumableGuards guards, IOptions<ConsumableOptions> options)
{
    private readonly ConsumableOptions settings = options.Value;

    public async Task<ServiceResult<MovementResult>> ReceiveAsync(ReceiveRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = guards.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);
        if (guards.CheckBackdate(date) is string backdate) return Fail(backdate);

        var source = T.Source(r.Source);
        if (source is null) return Fail("Source must be Weld Shop or Tool Crib.");

        var (receiver, receiverError) = G.ReceiverName(r.ReceivedByWelderId, await guards.ReceiverAsync(r.ReceivedByWelderId, ct));
        if (receiver is null) return Fail(receiverError!);

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
            var (n, itemError) = items.Normalize(r.NewItem, await items.SpecificationNamesAsync(ct));
            if (n is null) return Fail(itemError!);
            newItem = n;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        ConsumableItem? item;
        if (r.ItemId is int itemId)
        {
            item = await store.ItemAsync(itemId, ct);
            if (item is null) return ItemNotFound();
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
            (ConsumableItemService.LookupSize, item.Diameter),
            (ConsumableItemService.LookupType, item.Specification),
        ], ct);

        var lotId = await store.FindLotAsync(item.Id, brand, lotNo, ct);
        if (lotId is null)
        {
            var lot = new ConsumableItemLot { ItemId = item.Id, Brand = brand, LotNumber = lotNo };
            await store.InsertLotAsync(lot, ct);
            lotId = lot.Id;
        }

        var txnNo = await ledger.NextTxnNoAsync(ct);
        await store.AddMovementsAsync([new ConsumableMovement
        {
            TxnNo = txnNo,
            TxnType = Cat.TxnReceive,
            TxnDate = date,
            LotId = lotId.Value,
            QuantityKg = qty,
            ToStage = Cat.Normal,
            Source = source,
            Requestor = receiver,
            Remarks = T.FreeText(r.Remarks, 500),
            CreatedBy = user,
        }], ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    public async Task<ServiceResult<MovementResult>> TransferAsync(TransferRequest r, string? enteredBy, CancellationToken ct)
    {
        var (user, date, error) = guards.Common(enteredBy, r.TxnDate);
        if (error is not null) return Fail(error);
        if (guards.CheckBackdate(date) is string backdate) return Fail(backdate);

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
        if (item is null) return ItemNotFound();
        if (item.IsElectrode && !settings.AllowElectrodeDirectTransfer)
            return Fail("Electrodes reach Activated storage through Baking and the holding ovens, not by direct transfer.");

        var available = from == Cat.Normal
            ? await ledger.NormalLotsAsync(item.Id, ct)
            : await guards.BinLotsAsync(item.Id, null, ct);
        var (lines, takeError) = G.Take(available, r.LotId, qty, $"{from} storage");
        if (lines is null) return Fail(takeError!, StatusCodes.Status409Conflict);

        var txnNo = await ledger.NextTxnNoAsync(ct);
        await store.AddMovementsAsync(lines.Select(line => new ConsumableMovement
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
        }), ct);
        await tx.CommitAsync(ct);

        return Ok(await ResultAsync(txnNo, item.Id, [], null, ct));
    }

    private async Task<MovementResult> ResultAsync(
        string txnNo, int itemId, List<ResidualLot> residuals, string? warning, CancellationToken ct)
    {
        var (lines, _) = await store.TransactionsAsync(new MovementQuery(TxnNo: txnNo), ct);
        return new MovementResult(txnNo, lines, await ledger.StageBalanceAsync(itemId, ct), residuals, warning);
    }

    private static ServiceResult<MovementResult> Ok(MovementResult value) => ServiceResult<MovementResult>.Ok(value);

    private static ServiceResult<MovementResult> Fail(string error, int status = StatusCodes.Status400BadRequest)
        => ServiceResult<MovementResult>.Fail(error, status);

    private static ServiceResult<MovementResult> ItemNotFound() => Fail("Consumable not found.", StatusCodes.Status404NotFound);
}
