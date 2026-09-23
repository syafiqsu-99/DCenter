namespace DCenter.Server.Models;

public record ServiceResult<T>(T? Value, int Status, string? Error)
{
    public bool Succeeded => Error is null;
    public static ServiceResult<T> Ok(T value) => new(value, StatusCodes.Status200OK, null);
    public static ServiceResult<T> Fail(string error, int status = StatusCodes.Status400BadRequest) => new(default, status, error);
}

public record ConsumableCatalogDto(IReadOnlyList<string> Types, IReadOnlyList<string> Locations);

public record ConsumableDto(
    int Id, string ConsumableType, string Manufacturer, string Specification, string Diameter,
    string DiaSpec, decimal MinStockKg, bool IsActive, decimal BalanceKg);

public record ConsumableUpsert(
    string? ConsumableType, string? Manufacturer, string? Specification, string? Diameter,
    decimal MinStockKg, bool IsActive = true);

public record LotOption(int Id, string LotNumber);

public record StockLotOption(
    int LotId, int ConsumableId, string ConsumableType, string Manufacturer, string Specification,
    string Diameter, string DiaSpec, string LotNumber, string Location, decimal BalanceKg, DateOnly FirstReceivedOn);

public record ReceiveRequest(
    DateOnly? TxnDate, string? Location, string? Requestor, int? ConsumableId, ConsumableUpsert? NewConsumable,
    string? LotNumber, decimal QuantityKg, string? Remarks);

public record IssueRequest(
    DateOnly? TxnDate, string? Location, string? Requestor, int LotId, decimal QuantityKg, string? Remarks);

public record VoidRequest(string? Remarks);

public record TransactionDto(
    int Id, string TxnType, DateOnly TxnDate, DateTime CreatedAt, string Location, int ConsumableId,
    string ConsumableType, string Manufacturer, string Specification, string Diameter, string DiaSpec,
    int LotId, string LotNumber, decimal QuantityKg, string? Requestor, string? ReferenceNo,
    string? Remarks, bool IsVoided, int? VoidsTxnId);

public record MovementResult(TransactionDto Transaction, decimal BalanceKg);

public record TransactionQuery(
    DateOnly? From, DateOnly? To, string? Type, string? Location, string? Q, int? LotId,
    int Skip = 0, int Take = 100);

public record TransactionPage(List<TransactionDto> Items, int Total);

public record TakeDto(int Id, DateOnly TxnDate, decimal QuantityKg, string? Requestor);

public record StockCardRow(
    int LotId, int ConsumableId, string? Requestor, DateOnly Date, string Source, string ConsumableType,
    string Brand, string Diameter, string Specification, string LotNumber, string DiaSpec,
    decimal ReceiveQtyKg, List<TakeDto> Takes, decimal IssuedKg, decimal BalanceKg,
    decimal MinStockKg, bool IsLow, DateTime LastMovementAt);

public record DashboardKpis(
    decimal TotalInventoryKg, decimal ReceivedThisMonthKg, decimal IssuedThisMonthKg, int LowStockCount,
    string MonthLabel, DateOnly MonthStart, DateOnly Today);

public record MonthInOut(string Month, decimal InKg, decimal OutKg);

public record TypeSeries(string ConsumableType, List<decimal> Values);

public record ConsumableBalanceRow(
    int ConsumableId, string ConsumableType, string Brand, string DiaSpec, decimal BalanceKg,
    decimal MinStockKg, decimal ShortfallKg, DateOnly? LastIssuedOn, bool IsLow);

public record DashboardDto(
    DashboardKpis Kpis, List<MonthInOut> InOut, List<TypeSeries> Consumption,
    List<ConsumableBalanceRow> Balances, List<ConsumableBalanceRow> LowStock, List<TransactionDto> Recent);

public record ImportError(int Line, string Message);

public record ImportResult(int Added, int Skipped, List<ImportError> Errors);
