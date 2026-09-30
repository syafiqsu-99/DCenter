namespace DCenter.Server.Models;

public record WorkOrderSummary(string WorkOrderNumber, string? AssemblyItem, string? AssemblyDesc, decimal? Qty);

public record WorkOrderSearchResponse(List<WorkOrderSummary> Items, bool HasMore);

public record WorkOrderHeader(string WorkOrderNumber, string? PartNo, string? Description);

public record BomChildrenRequest(List<string>? Items);

public record BomLinkDto(string Item, string Component, string? ComponentDesc);

public record WelderSignOff(string? WelderName, string? WelderNo, string? DisplayName, DateOnly? Date);

public record ReportSignOff(
    string? EngineerName, DateOnly? EngineerDate, string? QaName, DateOnly? QaDate, List<WelderSignOff>? Welders);
