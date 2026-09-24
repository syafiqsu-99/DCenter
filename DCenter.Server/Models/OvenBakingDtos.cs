namespace DCenter.Server.Models;

public record SendToBakeRequest(
    DateOnly? BakingDate, int ItemId, int? LotId, decimal QuantityKg, string? PersonInCharge, string? Remarks);

public record BakingTimesRequest(List<int>? Ids, DateTime? At);

public record BakingUpdate(
    string? PersonInCharge, DateOnly? BakingDate, DateTime? BakeStart, DateTime? BakeStop,
    DateTime? RebakeStart, DateTime? RebakeStop, string? Remarks);

public record PlaceRequest(
    DateOnly? HoldingDate, int BakingRecordId, int? CompartmentId, bool FinishedAfterBaking, int? WelderId,
    decimal QuantityKg, bool TakeAll, string? Remarks);

public record BakingRecordDto(
    int Id, string BakingNo, int ItemId, string Category, string DiaSpec, string? HoldingOvenType,
    int LotId, string Brand, string LotNumber, decimal QuantityKg, decimal BalanceKg, string PersonInCharge,
    DateOnly BakingDate, DateTime? BakeStart, DateTime? BakeStop, DateTime? RebakeStart, DateTime? RebakeStop,
    string Status, string? Remarks, string? CreatedBy, DateTime CreatedAt);

public record BakingResult(string? TxnNo, List<BakingRecordDto> Records);

public record BakingQuery(
    DateOnly? From, DateOnly? To, string? Status, string? Q, bool OpenOnly = false, int Skip = 0, int Take = 100);

public record BakingPage(List<BakingRecordDto> Items, int Total);

public record HoldingRecordDto(
    int Id, string HoldingNo, DateOnly HoldingDate, int BakingRecordId, string BakingNo, int ItemId, string DiaSpec,
    int LotId, string Brand, string LotNumber, int? WelderId, string? WelderName, int? CompartmentId, string? CompartmentLabel,
    bool IsFinishedAfterBaking, decimal QuantityKg, string TxnNo, bool IsVoided, string? Remarks, string? CreatedBy,
    DateTime CreatedAt);

public record HoldingQuery(DateOnly? From, DateOnly? To, string? Q, int Skip = 0, int Take = 100);

public record HoldingPage(List<HoldingRecordDto> Items, int Total);

public record PlaceResult(HoldingRecordDto Holding, BakingRecordDto Record, string? Warning);

public record BinLotDto(
    int ItemId, string DiaSpec, string Category, int LotId, string Brand, string LotNumber, decimal Kg, DateTime? SinceAt,
    int? CompartmentId, string? HoldingOvenType, string Specification, string Diameter);

public record CompartmentDto(
    int Id, int OvenId, int Number, string Label, string Code, decimal TotalKg, DateTime? OldestSinceAt, List<BinLotDto> Contents);

public record OvenDto(int Id, string Name, string Code, string OvenType, List<CompartmentDto> Compartments);

public record OvenBoardDto(List<OvenDto> Ovens, List<BinLotDto> Unassigned);
