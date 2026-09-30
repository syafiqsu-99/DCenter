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

public static class FixedOvens
{
    public const int CompartmentsPerOven = 9;

    public sealed record Definition(int Id, string Name, string Code, string OvenType);

    public static readonly Definition[] Ovens =
    [
        new(1, "Alloy Steel Oven", "AS", StockCatalog.OvenAlloySteel),
        new(2, "Mild Steel Oven", "MS", StockCatalog.OvenMildSteel),
        new(3, "Ni Alloy Oven", "NI", StockCatalog.OvenNiAlloy),
        new(4, "Stainless Steel Oven", "SS", StockCatalog.OvenStainlessSteel),
    ];

    public static int CompartmentId(int ovenId, int number) => (ovenId - 1) * CompartmentsPerOven + number;
}
