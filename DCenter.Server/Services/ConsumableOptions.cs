namespace DCenter.Server.Services;

public class ConsumableOptions
{
    public const string Section = "Consumables";

    public decimal FinishThresholdKg { get; set; } = 0.5m;
    public bool AllowElectrodeDirectTransfer { get; set; }
    public int ReturnWindowDays { get; set; } = 7;
    public string? SupervisorPassword { get; set; }
    public int SupervisorSessionHours { get; set; } = 12;
}
