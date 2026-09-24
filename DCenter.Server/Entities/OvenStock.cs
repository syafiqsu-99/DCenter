namespace DCenter.Server.Entities;

public class Oven
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string OvenType { get; set; } = string.Empty;

    public List<OvenCompartment> Compartments { get; set; } = [];
}

public class OvenCompartment
{
    public int Id { get; set; }
    public int OvenId { get; set; }
    public Oven Oven { get; set; } = null!;
    public int Number { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class BakingRecord
{
    public int Id { get; set; }
    public string BakingNo { get; set; } = string.Empty;
    public int LotId { get; set; }
    public ConsumableItemLot Lot { get; set; } = null!;
    public decimal QuantityKg { get; set; }
    public string PersonInCharge { get; set; } = string.Empty;
    public DateOnly BakingDate { get; set; }
    public DateTime? BakeStart { get; set; }
    public DateTime? BakeStop { get; set; }
    public DateTime? RebakeStart { get; set; }
    public DateTime? RebakeStop { get; set; }
    public string Status { get; set; } = StockCatalog.StatusQueued;
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class HoldingRecord
{
    public int Id { get; set; }
    public string HoldingNo { get; set; } = string.Empty;
    public DateOnly HoldingDate { get; set; }
    public int BakingRecordId { get; set; }
    public BakingRecord BakingRecord { get; set; } = null!;
    public int? WelderId { get; set; }
    public Welder? Welder { get; set; }
    public string? WelderName { get; set; }
    public int? CompartmentId { get; set; }
    public OvenCompartment? Compartment { get; set; }
    public bool IsFinishedAfterBaking { get; set; }
    public decimal QuantityKg { get; set; }
    public string TxnNo { get; set; } = string.Empty;
    public bool IsVoided { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
