namespace DCenter.Server.Models;

public record WorkOrderSummary(string WorkOrderNumber, string? AssemblyItem, string? AssemblyDesc, decimal? Qty);
