namespace DCenter.Server.Models;

public class BomLink
{
    public string Item { get; set; } = string.Empty;
    public string Component { get; set; } = string.Empty;
    public string? ComponentDesc { get; set; }
}

public record BomLinkDto(string Item, string Component, string? ComponentDesc);
