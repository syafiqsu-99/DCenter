namespace DCenter.Server.Entities;

public class WpsItem
{
    public int Id { get; set; }
    public string WpsNo { get; set; } = string.Empty;
    public string? BaseMetal { get; set; }
    public string? Process { get; set; }
    public string PNo { get; set; } = string.Empty;
}