namespace DCenter.Server.Models;

public class BomTreeRow
{
    public string RootItem { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? ParentItem { get; set; }
    public string Component { get; set; } = string.Empty;
    public string? ComponentDesc { get; set; }
    public string Path { get; set; } = string.Empty;
}
