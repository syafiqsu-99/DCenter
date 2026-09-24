namespace DCenter.Server.Models;

public record CountLineDto(
    string Key, int ItemId, string Category, string Specification, string Diameter, string DiaSpec, int LotId, string Brand,
    string LotNumber, string Stage, int? CompartmentId, string Location, decimal SystemKg);

public record CountSheetDto(string Scope, string? Category, DateTime GeneratedAt, List<CountLineDto> Lines);

public record CountLineInput(int ItemId, int LotId, int? CompartmentId, decimal SystemKg, decimal CountedKg);

public record StockCountRequest(DateOnly? CountDate, string? Scope, string? Category, string? Remarks, List<CountLineInput>? Lines);

public record StockCountDto(
    int Id, string ReferenceNo, DateOnly CountDate, string Scope, string? Category, int LinesCounted, int LinesAdjusted,
    decimal GainKg, decimal LossKg, decimal NetKg, string? TxnNo, bool IsVoided, string? Remarks, string? CreatedBy,
    DateTime CreatedAt);

public record StockCountDetailDto(StockCountDto Count, List<TransactionDto> Adjustments);

public record StockCountPage(List<StockCountDto> Items, int Total);
