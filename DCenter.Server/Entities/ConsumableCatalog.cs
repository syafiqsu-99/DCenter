namespace DCenter.Server.Entities;

public static class ConsumableCatalog
{
    public const string TxnReceive = "Receive";
    public const string TxnIssue = "Issue";
    public const string TxnVoid = "Void";

    public const string Weldshop = "Weldshop";
    public const string ToolCrib = "Tool Crib";

    public const string BarePowderFiller = "Bare & Powder Filler";
    public const string ElectrodeFiller = "Electrode Filler";

    public const string OpeningReference = "OPENING";

    public const string LookupManufacturer = "Manuf";
    public const string LookupSize = "Size";
    public const string LookupType = "Type";

    public static readonly string[] TxnTypes = [TxnReceive, TxnIssue, TxnVoid];
    public static readonly string[] Locations = [Weldshop, ToolCrib];
    public static readonly string[] Types = [BarePowderFiller, ElectrodeFiller];

    public static string? FindType(string? raw)
    {
        var value = (raw ?? string.Empty).Trim();
        var exact = Types.FirstOrDefault(t => string.Equals(t, value, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var lower = value.ToLowerInvariant();
        if (lower.Contains("electrode")) return ElectrodeFiller;
        if (lower.Contains("bare") || lower.Contains("powder")) return BarePowderFiller;
        return null;
    }

    public static bool TryLocation(string? raw, out string? location)
    {
        location = null;
        var value = (raw ?? string.Empty).Trim();
        if (value.Length == 0 || value.Equals("All", StringComparison.OrdinalIgnoreCase)) return true;
        location = NormalizeLocation(value);
        return location is not null;
    }

    public static string? NormalizeLocation(string? raw)
    {
        var compact = (raw ?? string.Empty).Replace(" ", string.Empty);
        return Locations.FirstOrDefault(l =>
            string.Equals(l.Replace(" ", string.Empty), compact, StringComparison.OrdinalIgnoreCase));
    }

    public static string DiaSpec(string diameter, string specification) => $"{diameter} {specification}".Trim();
}
