namespace DCenter.Server.Models;

public record WorkOrderSearchResponse(List<WorkOrderSummary> Items, bool HasMore);

public record WorkOrderHeader(string WorkOrderNumber, string? PartNo, string? Description);

public record BomChildrenRequest(List<string>? Items);
