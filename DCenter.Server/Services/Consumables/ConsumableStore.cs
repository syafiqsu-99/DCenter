using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.Data.SqlClient;

namespace DCenter.Server.Services;

public sealed record LotRow(
    int Id, int ItemId, string Brand, string LotNumber, DateTime CreatedAt, string Category, string Specification,
    string Diameter, string? HoldingOvenType, decimal MinStockKg, bool IsActive);

public sealed record CompartmentRow(int Id, string Label, string OvenType, int Number);

// One oven and one of its compartments (compartment columns are null for an oven without compartments).
public sealed record OvenCompartmentRow(
    int OvenId, string Name, string Code, string OvenType, int? CompartmentId, int? Number, string? Label, string? FullLabel);

public sealed record BakingDetailRow(
    int Id, string BakingNo, int LotId, decimal QuantityKg, string PersonInCharge, DateOnly BakingDate,
    DateTime? BakeStart, DateTime? BakeStop, DateTime? RebakeStart, DateTime? RebakeStop, string Status, string? Remarks,
    string? CreatedBy, DateTime CreatedAt, int ItemId, string Category, string Diameter, string Specification,
    string? HoldingOvenType, string Brand, string LotNumber)
{
    public BakingRecord ToRecord() => new()
    {
        Id = Id, BakingNo = BakingNo, LotId = LotId, QuantityKg = QuantityKg, PersonInCharge = PersonInCharge, BakingDate = BakingDate,
        BakeStart = BakeStart, BakeStop = BakeStop, RebakeStart = RebakeStart, RebakeStop = RebakeStop, Status = Status,
        Remarks = Remarks, CreatedBy = CreatedBy, CreatedAt = CreatedAt,
    };
}

public sealed record StockCountRow(
    int Id, string ReferenceNo, DateOnly CountDate, string Scope, string? Category, int LinesCounted, int LinesAdjusted,
    decimal GainKg, decimal LossKg, string? TxnNo, bool IsVoided, string? Remarks, string? CreatedBy, DateTime CreatedAt);

// Filters for the transaction list; Sort picks the order (see SP_Movement_List).
public sealed record MovementQuery(
    string? TxnNo = null, bool LiveOnly = false, int? WelderId = null, DateTime? CreatedFrom = null, string? TxnType = null,
    bool ExcludeVoidEntries = false, string? ReferenceNo = null, DateOnly? From = null, DateOnly? To = null, string? Stage = null,
    string? Category = null, int? ItemId = null, int? LotId = null, int? CompartmentId = null, int? BakingRecordId = null,
    IReadOnlyList<string>? Terms = null, MovementSort Sort = MovementSort.Id, int Skip = 0, int Take = int.MaxValue);

public enum MovementSort { Id = 1, IdDescending = 2, Newest = 3, TypeSpecLot = 4 }

// Every consumables read and write outside the stock ledger totals, as stored procedure calls on the
// request's connection (and its open transaction).
public class ConsumableStore(StoredProcedures sp)
{
    public async Task<List<ConsumableItem>> ItemsAsync(IEnumerable<int>? ids, string? category, CancellationToken ct)
        => (await ItemRowsAsync(ct, Sql.IdList("@Ids", ids), Sql.NVarChar("@Category", category, 30))).Select(r => r.ToItem()).ToList();

    public async Task<ConsumableItem?> ItemAsync(int id, CancellationToken ct)
        => (await ItemsAsync([id], null, ct)).FirstOrDefault();

    public async Task<List<ConsumableItem>> SearchItemsAsync(IEnumerable<string> terms, string? category, bool activeOnly, int take, CancellationToken ct)
        => (await ItemRowsAsync(ct, Sql.TextList("@Terms", terms), Sql.NVarChar("@Category", category, 30), Sql.Bit("@ActiveOnly", activeOnly),
            Sql.Int("@Take", take))).Select(r => r.ToItem()).ToList();

    public async Task<ConsumableItem?> FindItemAsync(string specification, string diameter, int excludeId, CancellationToken ct)
        => (await ItemRowsAsync(ct, Sql.NVarChar("@Specification", specification, 100), Sql.NVarChar("@Diameter", diameter, 30),
            Sql.Int("@ExcludeId", excludeId), Sql.Int("@Take", 1))).Select(r => r.ToItem()).FirstOrDefault();

    // Inserts the consumable when its Id is 0 (and sets the Id), otherwise updates it.
    public async Task SaveItemAsync(ConsumableItem item, CancellationToken ct)
        => item.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_Item_Save", ct,
            Sql.Int("@Id", item.Id == 0 ? null : item.Id),
            Sql.NVarChar("@Category", item.Category, 30),
            Sql.NVarChar("@Specification", item.Specification, 100),
            Sql.NVarChar("@Diameter", item.Diameter, 30),
            Sql.Decimal("@MinStockKg", item.MinStockKg, 10, 2),
            Sql.Decimal("@ActivatedMinKg", item.ActivatedMinKg, 10, 2),
            Sql.Decimal("@FinishThresholdKg", item.FinishThresholdKg, 10, 2),
            Sql.NVarChar("@HoldingOvenType", item.HoldingOvenType, 30),
            Sql.Bit("@IsActive", item.IsActive),
            Sql.DateTime2("@CreatedAt", item.CreatedAt)));

    public Task DeleteItemAsync(int id, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_Item_Delete", ct, Sql.Int("@Id", id)));

    public async Task<(int Movements, int Bakings)> ItemHistoryAsync(int itemId, CancellationToken ct)
    {
        var row = (await ItemRowsAsync(ct, Sql.IdList("@Ids", [itemId]), Sql.Bit("@WithHistory", true))).FirstOrDefault();
        return (row?.MovementCount ?? 0, row?.BakingCount ?? 0);
    }

    public async Task<List<int>> StockedItemIdsAsync(CancellationToken ct)
        => (await ItemRowsAsync(ct, Sql.Bit("@WithHistory", true))).Where(r => r.MovementCount > 0).Select(r => r.Id).ToList();

    // Specification spellings: the dropdown list first (in its order), then the ones consumables already use.
    public async Task<List<string>> SpecificationNamesAsync(string lookupCategory, CancellationToken ct)
    {
        var lookups = (await LookupsAsync(lookupCategory, ct)).OrderBy(l => l.SortOrder).ThenBy(l => l.Id).Select(l => l.Value);
        var items = (await ItemRowsAsync(ct)).OrderBy(r => r.Id).Select(r => r.Specification);
        return [.. lookups, .. items];
    }

    public Task<List<LotRow>> LotsAsync(IEnumerable<int>? ids, IEnumerable<int>? itemIds, string? category, CancellationToken ct)
        => sp.QueryAsync<LotRow>("SP_Lot_List", ct,
            Sql.IdList("@Ids", ids), Sql.IdList("@ItemIds", itemIds), Sql.NVarChar("@Category", category, 30));

    public async Task<int?> FindLotAsync(int itemId, string brand, string lotNumber, CancellationToken ct)
        => (await sp.QueryAsync<LotRow>("SP_Lot_List", ct,
            Sql.IdList("@ItemIds", [itemId]), Sql.NVarChar("@Brand", brand, 100), Sql.NVarChar("@LotNumber", lotNumber, 60)))
            .Select(l => (int?)l.Id).FirstOrDefault();

    public async Task InsertLotAsync(ConsumableItemLot lot, CancellationToken ct)
        => lot.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_Lot_Save", ct,
            Sql.Int("@ItemId", lot.ItemId), Sql.NVarChar("@Brand", lot.Brand, 100), Sql.NVarChar("@LotNumber", lot.LotNumber, 60),
            Sql.DateTime2("@CreatedAt", lot.CreatedAt)));

    public Task<List<ConsumableMovement>> MovementsAsync(string? txnNo, bool notVoidedOnly, IEnumerable<int>? ids, CancellationToken ct)
        => sp.EntitiesAsync<ConsumableMovement>("SP_Movement_Get", ct,
            Sql.NVarChar("@TxnNo", txnNo, 20), Sql.Bit("@NotVoidedOnly", notVoidedOnly), Sql.IdList("@Ids", ids));

    // Marks the given lines voided and adds the new ledger lines in the given order, in one statement batch.
    public Task AddMovementsAsync(IEnumerable<ConsumableMovement> movements, CancellationToken ct, IEnumerable<int>? voidIds = null)
    {
        var rows = movements.Select((m, seq) => new
        {
            Seq = seq, m.TxnNo, m.TxnType, m.TxnDate, m.LotId, m.QuantityKg, m.FromStage, m.ToStage, m.FromCompartmentId,
            m.ToCompartmentId, m.BakingRecordId, m.Source, m.Requestor, m.WelderId, m.Reason, m.CountedQtyKg, m.ReferenceNo,
            m.Remarks, m.IsVoided, m.VoidsMovementId, m.CreatedBy, m.CreatedAt,
        }).ToList();
        if (rows.Count == 0 && voidIds is null) return Task.CompletedTask;
        return StoredProcedures.Write(sp.ExecuteAsync("SP_Movement_Save", ct,
            Sql.Json("@Rows", rows), Sql.IdList("@VoidIds", voidIds)));
    }

    public async Task<(List<TransactionDto> Rows, int Total)> TransactionsAsync(MovementQuery q, CancellationToken ct)
    {
        var total = Sql.OutputInt("@Total");
        var rows = await sp.QueryAsync<TransactionDto>("SP_Movement_List", ct,
            Sql.NVarChar("@TxnNo", q.TxnNo, 20),
            Sql.Bit("@LiveOnly", q.LiveOnly),
            Sql.Int("@WelderId", q.WelderId),
            Sql.DateTime2("@CreatedFrom", q.CreatedFrom),
            Sql.NVarChar("@TxnType", q.TxnType, 20),
            Sql.Bit("@ExcludeVoidEntries", q.ExcludeVoidEntries),
            Sql.NVarChar("@ReferenceNo", q.ReferenceNo, 60),
            Sql.Date("@From", q.From),
            Sql.Date("@To", q.To),
            Sql.NVarChar("@Stage", q.Stage, 20),
            Sql.NVarChar("@Category", q.Category, 30),
            Sql.Int("@ItemId", q.ItemId),
            Sql.Int("@LotId", q.LotId),
            Sql.Int("@CompartmentId", q.CompartmentId),
            Sql.Int("@BakingRecordId", q.BakingRecordId),
            Sql.TextList("@Terms", q.Terms),
            Sql.Int("@Sort", (int)q.Sort),
            Sql.Int("@Skip", q.Skip),
            Sql.Int("@Take", q.Take),
            total);
        return (rows, (int)total.Value);
    }

    public Task<List<OvenCompartmentRow>> OvenCompartmentsAsync(IEnumerable<int>? ids, CancellationToken ct)
        => sp.QueryAsync<OvenCompartmentRow>("SP_Oven_Compartments", ct, Sql.IdList("@Ids", ids));

    public async Task<List<CompartmentRow>> CompartmentsAsync(IEnumerable<int>? ids, CancellationToken ct)
        => (await OvenCompartmentsAsync(ids, ct))
            .Where(r => r.CompartmentId is not null)
            .Select(r => new CompartmentRow(r.CompartmentId!.Value, r.FullLabel!, r.OvenType, r.Number!.Value))
            .OrderBy(c => c.Id)
            .ToList();

    public async Task<List<BakingRecord>> BakingRecordsAsync(IEnumerable<int> ids, CancellationToken ct)
        => (await BakingDetailsAsync(ids, ct)).Select(r => r.ToRecord()).ToList();

    // Baking records with their lot and consumable, in Id order.
    public async Task<List<BakingDetailRow>> BakingDetailsAsync(IEnumerable<int> ids, CancellationToken ct)
        => (await BakingRowsAsync(ct, Sql.IdList("@Ids", ids))).Rows.OrderBy(r => r.Id).ToList();

    // Newest first, with the total count.
    public Task<(List<BakingDetailRow> Rows, int Total)> SearchBakingAsync(
        DateOnly? from, DateOnly? to, string? status, IEnumerable<string>? statuses, IEnumerable<string> terms, int skip, int take,
        CancellationToken ct)
        => BakingRowsAsync(ct, Sql.Date("@From", from), Sql.Date("@To", to), Sql.NVarChar("@Status", status, 20),
            Sql.TextList("@Statuses", statuses), Sql.TextList("@Terms", terms), Sql.Int("@Skip", skip), Sql.Int("@Take", take));

    // The requested record of the lot, or the lot's latest finished bake; never a cancelled one.
    public async Task<BakingRecord?> RebakeRecordAsync(int lotId, int? requestedId, CancellationToken ct)
        => (await BakingRowsAsync(ct,
                Sql.IdList("@Ids", requestedId is int id ? [id] : null), Sql.Int("@LotId", lotId),
                Sql.NVarChar("@ExcludeStatus", StockCatalog.StatusCancelled, 20), Sql.Bit("@BakeStoppedOnly", requestedId is null),
                Sql.Int("@Take", 1)))
            .Rows.Select(r => r.ToRecord()).FirstOrDefault();

    // Inserts the records without an Id (setting their Ids, in order) and updates the others.
    public async Task SaveBakingAsync(IReadOnlyList<BakingRecord> records, CancellationToken ct)
    {
        if (records.Count == 0) return;
        var rows = records.Select((r, seq) => new
        {
            Seq = seq, Id = r.Id == 0 ? (int?)null : r.Id, r.BakingNo, r.LotId, r.QuantityKg, r.PersonInCharge, r.BakingDate,
            r.BakeStart, r.BakeStop, r.RebakeStart, r.RebakeStop, r.Status, r.Remarks, r.CreatedBy, r.CreatedAt,
        });
        var ids = await StoredProcedures.Write(sp.QueryAsync<int>("SP_Baking_Save", ct, Sql.Json("@Rows", rows)));
        var added = records.Where(r => r.Id == 0).ToList();
        for (var i = 0; i < added.Count; i++) added[i].Id = ids[i];
    }

    public async Task AddHoldingAsync(HoldingRecord h, CancellationToken ct)
        => h.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_Holding_Save", ct,
            Sql.NVarChar("@HoldingNo", h.HoldingNo, 20), Sql.Date("@HoldingDate", h.HoldingDate),
            Sql.Int("@BakingRecordId", h.BakingRecordId), Sql.Int("@WelderId", h.WelderId),
            Sql.NVarChar("@WelderName", h.WelderName, 200), Sql.Int("@CompartmentId", h.CompartmentId),
            Sql.Bit("@IsFinishedAfterBaking", h.IsFinishedAfterBaking), Sql.Decimal("@QuantityKg", h.QuantityKg, 10, 2),
            Sql.NVarChar("@TxnNo", h.TxnNo, 20), Sql.NVarChar("@Remarks", h.Remarks, 500),
            Sql.NVarChar("@CreatedBy", h.CreatedBy, 100), Sql.DateTime2("@CreatedAt", h.CreatedAt)));

    public async Task<(List<HoldingRecordDto> Rows, int Total)> SearchHoldingsAsync(
        int? id, DateOnly? from, DateOnly? to, IEnumerable<string> terms, int skip, int take, CancellationToken ct)
    {
        var total = Sql.OutputInt("@Total");
        var rows = await sp.QueryAsync<HoldingRecordDto>("SP_Holding_List", ct,
            Sql.Int("@Id", id), Sql.Date("@From", from), Sql.Date("@To", to), Sql.TextList("@Terms", terms),
            Sql.Int("@Skip", skip), Sql.Int("@Take", take), total);
        return (rows, (int)total.Value);
    }

    public Task VoidHoldingsAsync(string txnNo, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_Holding_Void", ct, Sql.NVarChar("@TxnNo", txnNo, 20)));

    public Task RelocateHoldingsAsync(int fromCompartmentId, int toCompartmentId, IEnumerable<int> lotIds, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_Holding_Relocate", ct,
            Sql.Int("@FromCompartmentId", fromCompartmentId), Sql.Int("@ToCompartmentId", toCompartmentId),
            Sql.IdList("@LotIds", lotIds)));

    public async Task AddStockCountAsync(StockCount c, CancellationToken ct)
        => c.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_StockCount_Save", ct,
            Sql.NVarChar("@ReferenceNo", c.ReferenceNo, 20), Sql.Date("@CountDate", c.CountDate), Sql.NVarChar("@Scope", c.Scope, 20),
            Sql.NVarChar("@Category", c.Category, 30), Sql.Int("@LinesCounted", c.LinesCounted), Sql.Int("@LinesAdjusted", c.LinesAdjusted),
            Sql.Decimal("@GainKg", c.GainKg, 10, 2), Sql.Decimal("@LossKg", c.LossKg, 10, 2), Sql.NVarChar("@TxnNo", c.TxnNo, 20),
            Sql.NVarChar("@Remarks", c.Remarks, 500), Sql.NVarChar("@CreatedBy", c.CreatedBy, 100), Sql.DateTime2("@CreatedAt", c.CreatedAt)));

    public async Task<(List<StockCountRow> Rows, int Total)> SearchStockCountsAsync(
        int? id, string? referenceNo, DateOnly? from, DateOnly? to, string? scope, int skip, int take, CancellationToken ct)
    {
        var total = Sql.OutputInt("@Total");
        var rows = await sp.QueryAsync<StockCountRow>("SP_StockCount_List", ct,
            Sql.Int("@Id", id), Sql.NVarChar("@ReferenceNo", referenceNo, 20), Sql.Date("@From", from), Sql.Date("@To", to),
            Sql.NVarChar("@Scope", scope, 20), Sql.Int("@Skip", skip), Sql.Int("@Take", take), total);
        return (rows, (int)total.Value);
    }

    public Task<List<LookupItem>> LookupsAsync(string category, CancellationToken ct)
        => sp.EntitiesAsync<LookupItem>("SP_Lookup_List", ct, Sql.NVarChar("@Category", category, 50));

    public async Task AddLookupsAsync(IReadOnlyList<LookupItem> lookups, CancellationToken ct)
    {
        if (lookups.Count == 0) return;
        var rows = lookups.Select((l, seq) => new { Seq = seq, Id = (int?)null, l.Category, l.Value, l.SortOrder, l.IsActive });
        await StoredProcedures.Write(sp.EntitiesAsync<LookupItem>("SP_Lookup_Save", ct, Sql.Json("@Rows", rows)));
    }

    private Task<List<ItemRow>> ItemRowsAsync(CancellationToken ct, params SqlParameter[] parameters)
        => sp.QueryAsync<ItemRow>("SP_Item_List", ct, parameters);

    private async Task<(List<BakingDetailRow> Rows, int Total)> BakingRowsAsync(
        CancellationToken ct, params SqlParameter[] parameters)
    {
        var total = Sql.OutputInt("@Total");
        var rows = await sp.QueryAsync<BakingDetailRow>("SP_Baking_List", ct, [.. parameters, total]);
        return (rows, (int)total.Value);
    }

    private sealed record ItemRow(
        int Id, string Category, string Specification, string Diameter, decimal MinStockKg, decimal ActivatedMinKg,
        decimal? FinishThresholdKg, string? HoldingOvenType, bool IsActive, DateTime CreatedAt, int MovementCount, int BakingCount)
    {
        public ConsumableItem ToItem() => new()
        {
            Id = Id, Category = Category, Specification = Specification, Diameter = Diameter, MinStockKg = MinStockKg,
            ActivatedMinKg = ActivatedMinKg, FinishThresholdKg = FinishThresholdKg, HoldingOvenType = HoldingOvenType,
            IsActive = IsActive, CreatedAt = CreatedAt,
        };
    }
}
