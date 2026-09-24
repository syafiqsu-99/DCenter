namespace DCenter.Server.Models;

public class WorkOrderDetail
{
    public string WoNumber { get; set; } = string.Empty;
    public string? AssemblyItem { get; set; }
    public string? ItemDesc { get; set; }
    public decimal? StartQuantity { get; set; }
}
