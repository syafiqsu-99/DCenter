namespace DCenter.Server.Models;

public class WorkOrderNode
{
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string? AssemblyItem { get; set; }
    public string? AssemblyDesc { get; set; }
    public decimal? Qty { get; set; }
    public int Level { get; set; }
    public string? ParentItem { get; set; }
    public string? Item { get; set; }
    public string? ItemDesc { get; set; }
    public string Path { get; set; } = string.Empty;
    public string? Mrn { get; set; }
    public string? MrnDesc { get; set; }
}
