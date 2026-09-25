namespace DCenter.Server.Entities;

public class ProcessTypeLink
{
    public int Id { get; set; }
    public string Process { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}