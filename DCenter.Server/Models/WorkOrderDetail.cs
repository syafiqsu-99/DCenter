namespace DCenter.Server.Models;

// Keyless representation of the source Work_Order_Detail table (read-only)
public class WorkOrderDetail
{
    public string WoNumber { get; set; } = string.Empty;
    public string? AssemblyItem { get; set; }
    public string? ItemDesc { get; set; }
    public decimal? StartQuantity { get; set; }
}
