using System.Globalization;
using System.Text.RegularExpressions;
using DCenter.Server.Entities;

namespace DCenter.Server.Services;

internal static partial class ConsumableText
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static decimal RoundKg(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public const decimal MaxKg = 99_999.99m;

    public const string MaxKgError = "Quantity cannot exceed 99,999.99 kg.";

    public static string? Trimmed(string? value)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    public static string? Collapse(string? value)
    {
        var v = Trimmed(value);
        return v is null ? null : Spaces().Replace(v, " ");
    }

    public static string? FreeText(string? value, int maxLength)
    {
        var v = Collapse(value);
        return v is null ? null : v.Length <= maxLength ? v : v[..maxLength];
    }

    public static string? Specification(string? raw) => Collapse(raw)?.ToUpperInvariant();

    public static decimal? Diameter(string? raw)
    {
        var v = Trimmed(raw)?.ToLowerInvariant().Replace("mm", string.Empty).Replace(',', '.').Replace(" ", string.Empty);
        if (v is null) return null;
        if (!decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)) return null;
        d = Math.Round(d, 2, MidpointRounding.AwayFromZero);
        return d > 0 && d < 100 ? d : null;
    }

    public static string FormatDiameter(decimal diameter) => StockCatalog.FormatDiameter(diameter);

    public static decimal DiameterSortKey(string diameter)
        => decimal.TryParse(diameter, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d) ? d : 0m;

    public static string? Category(string? raw)
    {
        var value = Trimmed(raw) ?? string.Empty;
        var exact = StockCatalog.Categories.FirstOrDefault(c => string.Equals(c, value, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;
        var lower = value.ToLowerInvariant();
        if (lower.Contains("electrode")) return StockCatalog.ElectrodeFiller;
        if (lower.Contains("bare") || lower.Contains("powder")) return StockCatalog.BarePowderFiller;
        return null;
    }

    public static bool TryCategoryFilter(string? raw, out string? category)
    {
        category = null;
        var value = Trimmed(raw);
        if (value is null || value.Equals("All", StringComparison.OrdinalIgnoreCase)) return true;
        category = Category(value);
        return category is not null;
    }

    public static string? Source(string? raw)
    {
        var compact = (raw ?? string.Empty).Replace(" ", string.Empty);
        return StockCatalog.Sources.FirstOrDefault(s =>
            string.Equals(s.Replace(" ", string.Empty), compact, StringComparison.OrdinalIgnoreCase));
    }

    public static string? Stage(string? raw)
        => StockCatalog.Stages.FirstOrDefault(s => string.Equals(s, raw?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? TxnType(string? raw)
        => StockCatalog.TxnTypes.FirstOrDefault(s => string.Equals(s, raw?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Reason(string? raw)
        => StockCatalog.AdjustReasons.FirstOrDefault(r => string.Equals(r, raw?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> Terms(string? q)
        => (q ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
