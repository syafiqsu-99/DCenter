using System.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;

namespace DCenter.Server.Services;

public sealed record LotRow(
    int Id, int ItemId, string Brand, string LotNumber, DateTime CreatedAt, string Category, string Specification,
    string Diameter, string? HoldingOvenType, decimal MinStockKg, bool IsActive);

public sealed record CompartmentRow(int Id, string Label, string OvenType, int Number);

public sealed record BakingDetailRow(
    int Id, string BakingNo, int ItemId, string Category, string Diameter, string Specification, string? HoldingOvenType,
    int LotId, string Brand, string LotNumber, decimal QuantityKg, string PersonInCharge, DateOnly BakingDate,
    DateTime? BakeStart, DateTime? BakeStop, DateTime? RebakeStart, DateTime? RebakeStop, string Status, string? Remarks,
    string? CreatedBy, DateTime CreatedAt);

public sealed record StockCountRow(
    int Id, string ReferenceNo, DateOnly CountDate, string Scope, string? Category, int LinesCounted, int LinesAdjusted,
    decimal GainKg, decimal LossKg, string? TxnNo, bool IsVoided, string? Remarks, string? CreatedBy, DateTime CreatedAt);

// Filters for the transaction list; Sort picks the order (see SP_DCenter_Movement_List).
public sealed record MovementQuery(
    string? TxnNo = null, int? WelderId = null, DateTime? CreatedFrom = null, string? TxnType = null,
    bool ExcludeVoidEntries = false, DateOnly? From = null, DateOnly? To = null, string? Stage = null,
    string? Category = null, int? ItemId = null, int? LotId = null, int? CompartmentId = null, int? BakingRecordId = null,
    IReadOnlyList<string>? Terms = null, MovementSort Sort = MovementSort.Id, int Skip = 0, int Take = int.MaxValue);

public enum MovementSort { Id = 1, IdDescending = 2, Newest = 3, TypeSpecLot = 4 }

// Every consumables read and write outside the stock ledger totals, as stored procedure calls on the
// request's connection (and its open transaction).
public class ConsumableStore(StoredProcedures sp)
{
    public Task<List<ConsumableItem>> ItemsAsync(IEnumerable<int>? ids, string? category, CancellationToken ct)
        => sp.EntitiesAsync<ConsumableItem>("SP_DCenter_Item_List", ct,
            Sql.Bit("@ByIds", ids is not null), Sql.IdList("@Ids", ids ?? []), Sql.NVarChar("@Category", category, 30));

    public async Task<ConsumableItem?> ItemAsync(int id, CancellationToken ct)
        => (await ItemsAsync([id], null, ct)).FirstOrDefault();

    public Task<List<ConsumableItem>> SearchItemsAsync(IEnumerable<string> terms, string? category, bool activeOnly, int take, CancellationToken ct)
        => sp.EntitiesAsync<ConsumableItem>("SP_DCenter_Item_Search", ct,
            Sql.TextList("@Terms", terms), Sql.NVarChar("@Category", category, 30), Sql.Bit("@ActiveOnly", activeOnly), Sql.Int("@Take", take));

    public async Task<ConsumableItem?> FindItemAsync(string specification, string diameter, int excludeId, CancellationToken ct)
        => (await sp.EntitiesAsync<ConsumableItem>("SP_DCenter_Item_FindBySpec", ct,
            Sql.NVarChar("@Specification", specification, 100), Sql.NVarChar("@Diameter", diameter, 30),
            Sql.Int("@ExcludeId", excludeId))).FirstOrDefault();

    // Inserts the consumable when its Id is 0 (and sets the Id), otherwise updates it.
    public async Task SaveItemAsync(ConsumableItem item, CancellationToken ct)
        => item.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_DCenter_Item_Save", ct,
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
        => StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Item_Delete", ct, Sql.Int("@Id", id)));

    public async Task<(int Movements, int Bakings)> ItemHistoryAsync(int itemId, CancellationToken ct)
    {
        var row = (await sp.QueryAsync<ItemHistory>("SP_DCenter_Item_History", ct, Sql.Int("@ItemId", itemId))).First();
        return (row.Movements, row.Bakings);
    }

    public Task<List<int>> StockedItemIdsAsync(CancellationToken ct)
        => sp.QueryAsync<int>("SP_DCenter_Item_StockedIds", ct);

    public Task<List<string>> SpecificationNamesAsync(string lookupCategory, CancellationToken ct)
        => sp.QueryAsync<string>("SP_DCenter_Item_SpecificationNames", ct, Sql.NVarChar("@LookupCategory", lookupCategory, 50));

    public Task<List<LotRow>> LotsAsync(IEnumerable<int>? ids, IEnumerable<int>? itemIds, string? category, CancellationToken ct)
        => sp.QueryAsync<LotRow>("SP_DCenter_Lot_List", ct,
            Sql.Bit("@ByIds", ids is not null), Sql.IdList("@Ids", ids ?? []),
            Sql.Bit("@ByItems", itemIds is not null), Sql.IdList("@ItemIds", itemIds ?? []),
            Sql.NVarChar("@Category", category, 30));

    public Task<int?> FindLotAsync(int itemId, string brand, string lotNumber, CancellationToken ct)
        => sp.FirstOrDefaultAsync<int?>("SP_DCenter_Lot_Find", ct,
            Sql.Int("@ItemId", itemId), Sql.NVarChar("@Brand", brand, 100), Sql.NVarChar("@LotNumber", lotNumber, 60));

    public async Task InsertLotAsync(ConsumableItemLot lot, CancellationToken ct)
        => lot.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_DCenter_Lot_Insert", ct,
            Sql.Int("@ItemId", lot.ItemId), Sql.NVarChar("@Brand", lot.Brand, 100), Sql.NVarChar("@LotNumber", lot.LotNumber, 60),
            Sql.DateTime2("@CreatedAt", lot.CreatedAt)));

    public Task<List<ConsumableMovement>> MovementsAsync(string? txnNo, bool notVoidedOnly, IEnumerable<int>? ids, CancellationToken ct)
        => sp.EntitiesAsync<ConsumableMovement>("SP_DCenter_Movement_Get", ct,
            Sql.NVarChar("@TxnNo", txnNo, 20), Sql.Bit("@NotVoidedOnly", notVoidedOnly),
            Sql.Bit("@ByIds", ids is not null), Sql.IdList("@Ids", ids ?? []));

    // Adds ledger lines in the given order.
    public async Task AddMovementsAsync(IEnumerable<ConsumableMovement> movements, CancellationToken ct)
    {
        var rows = new DataTable();
        foreach (var (name, type) in MovementColumns) rows.Columns.Add(name, type);
        foreach (var m in movements)
        {
            rows.Rows.Add(rows.Rows.Count, m.TxnNo, m.TxnType, m.TxnDate.ToDateTime(TimeOnly.MinValue), m.LotId, m.QuantityKg,
                Cell(m.FromStage), Cell(m.ToStage), Cell(m.FromCompartmentId), Cell(m.ToCompartmentId), Cell(m.BakingRecordId),
                Cell(m.Source), Cell(m.Requestor), Cell(m.WelderId), Cell(m.Reason), Cell(m.CountedQtyKg), Cell(m.ReferenceNo),
                Cell(m.Remarks), m.IsVoided, Cell(m.VoidsMovementId), Cell(m.CreatedBy), m.CreatedAt);
        }
        if (rows.Rows.Count == 0) return;
        await StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Movement_Insert", ct,
            Sql.Table("@Rows", "dbo.TT_DCenter_MovementRows", rows)));
    }

    public Task SetVoidedAsync(IEnumerable<int> ids, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Movement_SetVoided", ct, Sql.IdList("@Ids", ids)));

    public async Task<(List<TransactionDto> Rows, int Total)> TransactionsAsync(MovementQuery q, CancellationToken ct)
    {
        var total = Sql.OutputInt("@Total");
        var rows = await sp.QueryAsync<TransactionDto>("SP_DCenter_Movement_List", ct,
            Sql.NVarChar("@TxnNo", q.TxnNo, 20),
            Sql.Int("@WelderId", q.WelderId),
            Sql.DateTime2("@CreatedFrom", q.CreatedFrom),
            Sql.NVarChar("@TxnType", q.TxnType, 20),
            Sql.Bit("@ExcludeVoidEntries", q.ExcludeVoidEntries),
            Sql.Date("@From", q.From),
            Sql.Date("@To", q.To),
            Sql.NVarChar("@Stage", q.Stage, 20),
            Sql.NVarChar("@Category", q.Category, 30),
            Sql.Int("@ItemId", q.ItemId),
            Sql.Int("@LotId", q.LotId),
            Sql.Int("@CompartmentId", q.CompartmentId),
            Sql.Int("@BakingRecordId", q.BakingRecordId),
            Sql.TextList("@Terms", q.Terms ?? []),
            Sql.Int("@Sort", (int)q.Sort),
            Sql.Int("@Skip", q.Skip),
            Sql.Int("@Take", q.Take),
            total);
        return (rows, (int)total.Value);
    }

    public Task<List<CompartmentRow>> CompartmentsAsync(IEnumerable<int>? ids, CancellationToken ct)
        => sp.QueryAsync<CompartmentRow>("SP_DCenter_Compartment_List", ct,
            Sql.Bit("@ByIds", ids is not null), Sql.IdList("@Ids", ids ?? []));

    public Task<List<BakingRecord>> BakingRecordsAsync(IEnumerable<int> ids, CancellationToken ct)
        => sp.EntitiesAsync<BakingRecord>("SP_DCenter_Baking_Get", ct, Sql.IdList("@Ids", ids));

    public Task<List<BakingDetailRow>> BakingDetailsAsync(IEnumerable<int> ids, CancellationToken ct)
        => sp.QueryAsync<BakingDetailRow>("SP_DCenter_Baking_Details", ct, Sql.IdList("@Ids", ids));

    public async Task<(List<int> Ids, int Total)> SearchBakingAsync(
        DateOnly? from, DateOnly? to, string? status, IEnumerable<string>? statuses, IEnumerable<string> terms, int skip, int take,
        CancellationToken ct)
    {
        var total = Sql.OutputInt("@Total");
        var ids = await sp.QueryAsync<int>("SP_DCenter_Baking_Search", ct,
            Sql.Date("@From", from), Sql.Date("@To", to), Sql.NVarChar("@Status", status, 20),
            Sql.Bit("@ByStatuses", statuses is not null), Sql.TextList("@Statuses", statuses ?? []),
            Sql.TextList("@Terms", terms), Sql.Int("@Skip", skip), Sql.Int("@Take", take), total);
        return (ids, (int)total.Value);
    }

    public async Task<BakingRecord?> RebakeRecordAsync(int lotId, int? requestedId, CancellationToken ct)
        => (await sp.EntitiesAsync<BakingRecord>("SP_DCenter_Baking_ForRebake", ct,
            Sql.Int("@LotId", lotId), Sql.Int("@RequestedId", requestedId))).FirstOrDefault();

    // Inserts the records in order and sets their Ids.
    public async Task AddBakingRecordsAsync(IReadOnlyList<BakingRecord> records, CancellationToken ct)
    {
        var rows = new DataTable();
        rows.Columns.Add("Seq", typeof(int));
        rows.Columns.Add("BakingNo", typeof(string));
        rows.Columns.Add("LotId", typeof(int));
        rows.Columns.Add("QuantityKg", typeof(decimal));
        rows.Columns.Add("PersonInCharge", typeof(string));
        rows.Columns.Add("BakingDate", typeof(DateTime));
        rows.Columns.Add("Status", typeof(string));
        rows.Columns.Add("Remarks", typeof(string));
        rows.Columns.Add("CreatedBy", typeof(string));
        rows.Columns.Add("CreatedAt", typeof(DateTime));
        foreach (var r in records)
            rows.Rows.Add(rows.Rows.Count, r.BakingNo, r.LotId, r.QuantityKg, r.PersonInCharge, r.BakingDate.ToDateTime(TimeOnly.MinValue),
                r.Status, Cell(r.Remarks), Cell(r.CreatedBy), r.CreatedAt);
        var ids = await StoredProcedures.Write(sp.QueryAsync<int>("SP_DCenter_Baking_Insert", ct,
            Sql.Table("@Rows", "dbo.TT_DCenter_BakingRows", rows)));
        for (var i = 0; i < records.Count; i++) records[i].Id = ids[i];
    }

    public Task UpdateBakingAsync(BakingRecord r, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Baking_Update", ct,
            Sql.Int("@Id", r.Id), Sql.NVarChar("@PersonInCharge", r.PersonInCharge, 100), Sql.Date("@BakingDate", r.BakingDate),
            Sql.DateTime2("@BakeStart", r.BakeStart), Sql.DateTime2("@BakeStop", r.BakeStop),
            Sql.DateTime2("@RebakeStart", r.RebakeStart), Sql.DateTime2("@RebakeStop", r.RebakeStop),
            Sql.NVarChar("@Remarks", r.Remarks, 500)));

    public async Task SetBakingTimesAsync(IEnumerable<BakingRecord> records, CancellationToken ct)
    {
        var rows = new DataTable();
        rows.Columns.Add("Id", typeof(int));
        foreach (var name in (string[])["BakeStart", "BakeStop", "RebakeStart", "RebakeStop"]) rows.Columns.Add(name, typeof(DateTime));
        foreach (var r in records) rows.Rows.Add(r.Id, Cell(r.BakeStart), Cell(r.BakeStop), Cell(r.RebakeStart), Cell(r.RebakeStop));
        if (rows.Rows.Count == 0) return;
        await StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Baking_SetTimes", ct,
            Sql.Table("@Rows", "dbo.TT_DCenter_BakingTimes", rows)));
    }

    public async Task SetBakingStatusAsync(IEnumerable<(int Id, string Status)> statuses, CancellationToken ct)
    {
        var rows = new DataTable();
        rows.Columns.Add("Id", typeof(int));
        rows.Columns.Add("Status", typeof(string));
        foreach (var (id, status) in statuses) rows.Rows.Add(id, status);
        if (rows.Rows.Count == 0) return;
        await StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Baking_SetStatus", ct,
            Sql.Table("@Rows", "dbo.TT_DCenter_IdStatus", rows)));
    }

    public async Task AddHoldingAsync(HoldingRecord h, CancellationToken ct)
        => h.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_DCenter_Holding_Insert", ct,
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
        var rows = await sp.QueryAsync<HoldingRecordDto>("SP_DCenter_Holding_Search", ct,
            Sql.Int("@Id", id), Sql.Date("@From", from), Sql.Date("@To", to), Sql.TextList("@Terms", terms),
            Sql.Int("@Skip", skip), Sql.Int("@Take", take), total);
        return (rows, (int)total.Value);
    }

    public Task VoidHoldingsAsync(string txnNo, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Holding_VoidByTxnNo", ct, Sql.NVarChar("@TxnNo", txnNo, 20)));

    public Task RelocateHoldingsAsync(int fromCompartmentId, int toCompartmentId, IEnumerable<int> lotIds, CancellationToken ct)
        => StoredProcedures.Write(sp.ExecuteAsync("SP_DCenter_Holding_Relocate", ct,
            Sql.Int("@FromCompartmentId", fromCompartmentId), Sql.Int("@ToCompartmentId", toCompartmentId),
            Sql.IdList("@LotIds", lotIds)));

    public async Task AddStockCountAsync(StockCount c, CancellationToken ct)
        => c.Id = await StoredProcedures.Write(sp.ScalarAsync<int>("SP_DCenter_StockCount_Insert", ct,
            Sql.NVarChar("@ReferenceNo", c.ReferenceNo, 20), Sql.Date("@CountDate", c.CountDate), Sql.NVarChar("@Scope", c.Scope, 20),
            Sql.NVarChar("@Category", c.Category, 30), Sql.Int("@LinesCounted", c.LinesCounted), Sql.Int("@LinesAdjusted", c.LinesAdjusted),
            Sql.Decimal("@GainKg", c.GainKg, 10, 2), Sql.Decimal("@LossKg", c.LossKg, 10, 2), Sql.NVarChar("@TxnNo", c.TxnNo, 20),
            Sql.NVarChar("@Remarks", c.Remarks, 500), Sql.NVarChar("@CreatedBy", c.CreatedBy, 100), Sql.DateTime2("@CreatedAt", c.CreatedAt)));

    public async Task<(List<StockCountRow> Rows, int Total)> SearchStockCountsAsync(
        int? id, string? referenceNo, DateOnly? from, DateOnly? to, string? scope, int skip, int take, CancellationToken ct)
    {
        var total = Sql.OutputInt("@Total");
        var rows = await sp.QueryAsync<StockCountRow>("SP_DCenter_StockCount_Search", ct,
            Sql.Int("@Id", id), Sql.NVarChar("@ReferenceNo", referenceNo, 20), Sql.Date("@From", from), Sql.Date("@To", to),
            Sql.NVarChar("@Scope", scope, 20), Sql.Int("@Skip", skip), Sql.Int("@Take", take), total);
        return (rows, (int)total.Value);
    }

    public Task<List<LookupItem>> LookupsAsync(string category, CancellationToken ct)
        => sp.EntitiesAsync<LookupItem>("SP_DCenter_Lookup_List", ct, Sql.NVarChar("@Category", category, 50));

    public async Task AddLookupsAsync(IReadOnlyList<LookupItem> lookups, CancellationToken ct)
    {
        if (lookups.Count == 0) return;
        var rows = new DataTable();
        rows.Columns.Add("Seq", typeof(int));
        rows.Columns.Add("Id", typeof(int));
        rows.Columns.Add("Category", typeof(string));
        rows.Columns.Add("Value", typeof(string));
        rows.Columns.Add("SortOrder", typeof(int));
        rows.Columns.Add("IsActive", typeof(bool));
        foreach (var l in lookups) rows.Rows.Add(rows.Rows.Count, DBNull.Value, l.Category, l.Value, l.SortOrder, l.IsActive);
        await StoredProcedures.Write(sp.EntitiesAsync<LookupItem>("SP_DCenter_Lookup_Save", ct,
            Sql.Table("@Rows", "dbo.TT_DCenter_LookupRows", rows)));
    }

    private sealed record ItemHistory(int Movements, int Bakings);

    private static readonly (string Name, Type Type)[] MovementColumns =
    [
        ("Seq", typeof(int)), ("TxnNo", typeof(string)), ("TxnType", typeof(string)), ("TxnDate", typeof(DateTime)),
        ("LotId", typeof(int)), ("QuantityKg", typeof(decimal)), ("FromStage", typeof(string)), ("ToStage", typeof(string)),
        ("FromCompartmentId", typeof(int)), ("ToCompartmentId", typeof(int)), ("BakingRecordId", typeof(int)),
        ("Source", typeof(string)), ("Requestor", typeof(string)), ("WelderId", typeof(int)), ("Reason", typeof(string)),
        ("CountedQtyKg", typeof(decimal)), ("ReferenceNo", typeof(string)), ("Remarks", typeof(string)), ("IsVoided", typeof(bool)),
        ("VoidsMovementId", typeof(int)), ("CreatedBy", typeof(string)), ("CreatedAt", typeof(DateTime)),
    ];

    private static object Cell<T>(T? value) => (object?)value ?? DBNull.Value;
}
