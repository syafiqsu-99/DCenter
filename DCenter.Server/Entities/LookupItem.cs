namespace DCenter.Server.Entities;

// Category-keyed dropdown source. Category is one of: Process, Size, Type, Manuf.
public class LookupItem
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
