namespace DCenter.Server.Models;

public record ServiceResult<T>(T? Value, int Status, string? Error)
{
    public bool Succeeded => Error is null;
    public static ServiceResult<T> Ok(T value) => new(value, StatusCodes.Status200OK, null);
    public static ServiceResult<T> Fail(string error, int status = StatusCodes.Status400BadRequest) => new(default, status, error);
}

public record StockCatalogDto(
    IReadOnlyList<string> Categories, IReadOnlyList<string> Sources, IReadOnlyList<string> Stages,
    IReadOnlyList<string> AdjustReasons, IReadOnlyList<string> OvenTypes, decimal FinishThresholdKg,
    bool AllowElectrodeDirectTransfer, int ReturnWindowDays, IReadOnlyList<CompartmentCodeDto> Compartments);

public record CompartmentCodeDto(string Code, string OvenType);

public record ItemUpsert(
    string? Category, string? Specification, string? Diameter, decimal MinStockKg, decimal ActivatedMinKg,
    decimal? FinishThresholdKg, bool IsActive = true, string? HoldingOvenType = null);

public record ItemDto(
    int Id, string Category, string Specification, string Diameter, string DiaSpec, decimal MinStockKg,
    decimal ActivatedMinKg, decimal? FinishThresholdKg, bool IsActive, decimal NormalKg, decimal ActivatedKg, decimal TotalKg,
    string? HoldingOvenType);

public record LotOption(int Id, string Brand, string LotNumber);

public record LotLocationDto(int? CompartmentId, string Label, decimal Kg);

public record LotBalanceDto(
    int LotId, string Brand, string LotNumber, DateOnly? FirstReceivedOn, string? Source,
    decimal NormalKg, decimal BakingKg, decimal ActivatedKg, decimal TotalKg, List<LotLocationDto> Locations);

public record ItemBalanceDto(
    int ItemId, string Category, string Specification, string Diameter, string DiaSpec,
    decimal NormalKg, decimal BakingKg, decimal ActivatedKg, decimal TotalKg, decimal MinStockKg, decimal ActivatedMinKg,
    decimal FinishThresholdKg, bool IsLow, bool NeedsRefill, int LotCount, DateOnly? LastIssuedOn, bool IsActive,
    string? HoldingOvenType);

public record LotStockRow(
    int LotId, int ItemId, string Category, string Brand, string Diameter, string Specification, string LotNumber,
    string DiaSpec, DateOnly? Date, string? Source, string? ReceivedBy, decimal ReceiveQtyKg, decimal TakeKg,
    decimal NormalKg, decimal BakingKg, decimal ActivatedKg, decimal BalanceKg, bool IsLow);

public record ReceiveRequest(
    DateOnly? TxnDate, string? Source, string? ReceivedBy, int? ItemId, ItemUpsert? NewItem,
    string? Brand, string? LotNumber, decimal QuantityKg, string? Remarks);

public record TransferRequest(
    DateOnly? TxnDate, int ItemId, int? LotId, string? FromStage, string? ToStage, decimal QuantityKg, string? Remarks);

public record IssueRequest(
    DateOnly? TxnDate, int WelderId, int ItemId, int? LotId, decimal QuantityKg, bool TakeAll, string? Remarks,
    int? CompartmentId = null);

public record ReturnRequest(
    DateOnly? TxnDate, int WelderId, int ItemId, int? LotId, decimal QuantityKg, string? Remarks,
    int? CompartmentId = null, bool ForRebake = false, int? BakingRecordId = null);

public record FinishRequest(
    int ItemId, int? LotId, string? Stage, string? Reason, string? Remarks, int? CompartmentId = null);

public record AdjustRequest(
    DateOnly? TxnDate, int ItemId, int? LotId, string? Stage, decimal CountedQtyKg, string? Reason, string? Remarks,
    int? CompartmentId = null);

public record MoveRequest(
    DateOnly? TxnDate, int ItemId, int? LotId, int? FromCompartmentId, int ToCompartmentId, decimal QuantityKg,
    bool TakeAll, string? Remarks);

public record VoidRequest(string? Remarks);

public record TransactionDto(
    int Id, string TxnNo, string TxnType, DateOnly TxnDate, DateTime CreatedAt, string? CreatedBy,
    int ItemId, string Category, string Specification, string Diameter, string DiaSpec,
    int LotId, string Brand, string LotNumber, decimal QuantityKg, string? FromStage, string? ToStage,
    string? Source, string? Requestor, int? WelderId, string? WelderName, string? Reason, decimal? CountedQtyKg,
    string? ReferenceNo, string? Remarks, bool IsVoided, int? VoidsMovementId,
    int? FromCompartmentId, string? FromCompartment, int? ToCompartmentId, string? ToCompartment,
    int? BakingRecordId, string? BakingNo);

public record StageBalance(int ItemId, string DiaSpec, decimal NormalKg, decimal BakingKg, decimal ActivatedKg, decimal TotalKg);

public record ResidualLot(
    int ItemId, int LotId, string DiaSpec, string Brand, string LotNumber, string Stage, decimal BalanceKg,
    int? CompartmentId, string? CompartmentLabel);

public record MovementResult(
    string TxnNo, List<TransactionDto> Lines, StageBalance Balance, List<ResidualLot> Residuals, string? Warning);

public record TransactionQuery(
    DateOnly? From, DateOnly? To, string? Type, string? Stage, string? Category, int? ItemId, int? LotId,
    int? WelderId, int? CompartmentId, int? BakingRecordId, string? Q, int Skip = 0, int Take = 100);

public record TransactionPage(List<TransactionDto> Items, int Total);

public record ActivatedLotDto(int LotId, string Brand, string LotNumber, decimal ActivatedKg);

public record CounterBinDto(int? CompartmentId, string Label, decimal Kg, List<ActivatedLotDto> Lots);

public record CounterItemDto(
    int ItemId, string Category, string DiaSpec, decimal ActivatedKg, decimal ActivatedMinKg, decimal FinishThresholdKg,
    decimal PickedKg, decimal ReturnedKg, int? LastLotId, int? LastCompartmentId, List<ActivatedLotDto> Lots,
    List<CounterBinDto> Bins, string? HoldingOvenType);

public record DashboardKpis(
    decimal TotalKg, decimal NormalKg, decimal BakingKg, decimal ActivatedKg, decimal InHoldingKg,
    int OccupiedCompartments, int TotalCompartments, decimal ReceivedThisMonthKg, decimal NetConsumedThisMonthKg,
    int LowStockCount, int RefillCount, string MonthLabel, DateOnly MonthStart, DateOnly Today);

public record MonthInOut(string Month, decimal InKg, decimal OutKg, DateOnly Start);

public record ItemMonthUsageDto(
    int ItemId, string Category, string DiaSpec, decimal PickedKg, decimal ReturnedKg, decimal FinishedKg, decimal NetKg);

public record CategorySeries(string Category, List<decimal> Values);

public record ItemUsageDto(
    int ItemId, string Category, string DiaSpec, decimal ThisMonthKg, decimal LastMonthKg, decimal AverageMonthlyKg,
    decimal OnHandKg, decimal MinStockKg, decimal? CoverDays, bool IsLow);

public record NormalStockDto(
    int ItemId, string Category, string DiaSpec, decimal NormalKg, decimal BakingKg, decimal ActivatedKg, int LotCount,
    string? HoldingOvenType);

public record DashboardDto(
    DashboardKpis Kpis, List<MonthInOut> InOut, List<CategorySeries> Consumption, List<ItemBalanceDto> Balances,
    List<ItemUsageDto> ItemUsage);

public record WelderScopeUpdate(List<int>? Ids, string? UsageScope);
