namespace DCenter.Server.Models;

// One row per child part of a job, from the source work-order DB.
public class JobRow
{
    public string JobNumber { get; set; } = string.Empty;
    public string? AssemblyItem { get; set; }
    public string? ItemDesc { get; set; }
    public decimal? Qty { get; set; }
    public string? ChildPart { get; set; }
    public string? ComponentDesc { get; set; }
}
