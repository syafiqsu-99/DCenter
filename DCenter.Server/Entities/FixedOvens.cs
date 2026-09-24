namespace DCenter.Server.Entities;

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
