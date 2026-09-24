namespace DCenter.Server.Entities;

public class ConsumableItem
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Specification { get; set; } = string.Empty;
    public string Diameter { get; set; } = string.Empty;
    public decimal MinStockKg { get; set; }
    public decimal ActivatedMinKg { get; set; }
    public decimal? FinishThresholdKg { get; set; }
    public string? HoldingOvenType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<ConsumableItemLot> Lots { get; set; } = [];
}

public class ConsumableItemLot
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public ConsumableItem Item { get; set; } = null!;
    public string Brand { get; set; } = string.Empty;
    public string LotNumber { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public List<ConsumableMovement> Movements { get; set; } = [];
}

public class ConsumableMovement
{
    public int Id { get; set; }
    public string TxnNo { get; set; } = string.Empty;
    public string TxnType { get; set; } = string.Empty;
    public DateOnly TxnDate { get; set; }
    public int LotId { get; set; }
    public ConsumableItemLot Lot { get; set; } = null!;
    public decimal QuantityKg { get; set; }
    public string? FromStage { get; set; }
    public string? ToStage { get; set; }
    public int? FromCompartmentId { get; set; }
    public OvenCompartment? FromCompartment { get; set; }
    public int? ToCompartmentId { get; set; }
    public OvenCompartment? ToCompartment { get; set; }
    public int? BakingRecordId { get; set; }
    public BakingRecord? BakingRecord { get; set; }
    public string? Source { get; set; }
    public string? Requestor { get; set; }
    public int? WelderId { get; set; }
    public Welder? Welder { get; set; }
    public string? Reason { get; set; }
    public decimal? CountedQtyKg { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Remarks { get; set; }
    public bool IsVoided { get; set; }
    public int? VoidsMovementId { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
