namespace DCenter.Server.Entities;

public class StockCount
{
    public int Id { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public DateOnly CountDate { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int LinesCounted { get; set; }
    public int LinesAdjusted { get; set; }
    public decimal GainKg { get; set; }
    public decimal LossKg { get; set; }
    public string? TxnNo { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
