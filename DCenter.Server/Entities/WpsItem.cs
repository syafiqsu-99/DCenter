namespace DCenter.Server.Entities;

// WPS (Welding Procedure Specification) numbers, for autocomplete on the joint form.
public class WpsItem
{
    public int Id { get; set; }
    public string WpsNo { get; set; } = string.Empty;
    public string? Rev { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}