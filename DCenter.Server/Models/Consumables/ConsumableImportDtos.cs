namespace DCenter.Server.Models;

public record ImportRowDto(int Line, string Action, string? Category, string? DiaSpec, List<string> Messages);

public record ImportResultDto(
    bool Committed, int TotalRows, int Created, int Updated, int Unchanged, int Rejected, List<ImportRowDto> Rows,
    List<string> FileErrors);

public record StockImportRowDto(
    int Line, string Status, string? DiaSpec, string? Brand, string? LotNumber, decimal? QuantityKg, string? Location, List<string> Messages);

public record StockImportResultDto(
    bool Committed, int TotalRows, int Ready, int NewConsumables, int Rejected, int Skipped, string? TxnNo,
    List<StockImportRowDto> Rows, List<string> FileErrors);
