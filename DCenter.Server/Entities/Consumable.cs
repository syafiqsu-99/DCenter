namespace DCenter.Server.Entities;

public class Consumable
{
    public int Id { get; set; }
    public string ConsumableType { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Specification { get; set; } = string.Empty;
    public string Diameter { get; set; } = string.Empty;
    public decimal MinStockKg { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<ConsumableLot> Lots { get; set; } = [];
}

public class ConsumableLot
{
    public int Id { get; set; }
    public int ConsumableId { get; set; }
    public Consumable Consumable { get; set; } = null!;
    public string LotNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<ConsumableTransaction> Transactions { get; set; } = [];
}

public class ConsumableTransaction
{
    public int Id { get; set; }
    public int LotId { get; set; }
    public ConsumableLot Lot { get; set; } = null!;
    public string TxnType { get; set; } = string.Empty;
    public DateOnly TxnDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal QuantityKg { get; set; }
    public string? Requestor { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Remarks { get; set; }
    public bool IsVoided { get; set; }
    public int? VoidsTxnId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
