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

    private static readonly Regex MeshPattern = new(@"^(\d{1,4})/(\d{1,4})$", RegexOptions.CultureInvariant);

    public static string? Diameter(string? raw, string? category)
    {
        var v = Trimmed(raw)?.ToLowerInvariant().Replace("mm", string.Empty).Replace(',', '.').Replace(" ", string.Empty);
        if (v is null) return null;
        if (decimal.TryParse(v, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
        {
            d = Math.Round(d, 2, MidpointRounding.AwayFromZero);
            return d > 0 && d < 100 ? StockCatalog.FormatDiameter(d) : null;
        }
        if (category == StockCatalog.ElectrodeFiller) return null;
        var mesh = MeshPattern.Match(v.Replace("mesh", string.Empty));
        return mesh.Success ? $"{int.Parse(mesh.Groups[1].Value)}/{int.Parse(mesh.Groups[2].Value)}" : null;
    }

    public static decimal DiameterSortKey(string diameter)
    {
        if (decimal.TryParse(diameter, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d)) return d;
        var mesh = MeshPattern.Match(diameter);
        return mesh.Success ? 100m + int.Parse(mesh.Groups[1].Value) + int.Parse(mesh.Groups[2].Value) / 100000m : 1_000_000m;
    }

    public const string StandardizedPrefix = "Standardized:";

    public static bool IsNote(string message)
        => message.StartsWith(WarningPrefix, StringComparison.Ordinal) || message.StartsWith(StandardizedPrefix, StringComparison.Ordinal);

    public const string WarningPrefix = "Warning:";

    public static (string? Value, string? Suggestion) MatchOption(string? raw, IReadOnlyList<string> options)
    {
        var key = OptionKey(raw);
        if (key.Length == 0) return (null, null);
        var exact = options.FirstOrDefault(o => OptionKey(o) == key);
        if (exact is not null) return (exact, null);

        var close = options
            .Select(o => (Option: o, Distance: EditDistance(OptionKey(o), key)))
            .Where(x => x.Distance <= 2)
            .OrderBy(x => x.Distance)
            .ToList();
        if (close.Count == 1 || (close.Count > 1 && close[0].Distance < close[1].Distance)) return (null, close[0].Option);

        var prefixed = options.Where(o => OptionKey(o).StartsWith(key, StringComparison.Ordinal)).ToList();
        return (null, prefixed.Count == 1 ? prefixed[0] : null);
    }

    public static string OptionError(string label, string? raw, IReadOnlyList<string> options, string? suggestion)
        => $"{label} \"{raw}\" is not valid" + (suggestion is null ? "." : $" — did you mean \"{suggestion}\"?")
           + $" Allowed: {string.Join(", ", options)}.";

    public static string? Standardized(string label, string? raw, string? value)
        => raw is not null && value is not null && !string.Equals(raw.Trim(), value, StringComparison.Ordinal)
            ? $"{StandardizedPrefix} {label} \"{raw.Trim()}\" → \"{value}\""
            : null;

    public static string TitleCase(string value)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(value.ToLowerInvariant());

    private static string OptionKey(string? value)
        => new string((value ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static int EditDistance(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            previous = current;
        }
        return previous[b.Length];
    }

    public static string? Category(string? raw)
    {
        var value = Trimmed(raw) ?? string.Empty;
        var exact = MatchOption(value, StockCatalog.Categories).Value;
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

    public static string? Source(string? raw) => MatchOption(raw, StockCatalog.Sources).Value;

    public static string? Stage(string? raw) => MatchOption(raw, StockCatalog.Stages).Value;

    public static string? TxnType(string? raw)
        => StockCatalog.TxnTypes.FirstOrDefault(s => string.Equals(s, raw?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string? Reason(string? raw)
        => StockCatalog.AdjustReasons.FirstOrDefault(r => string.Equals(r, raw?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<string> Terms(string? q)
        => (q ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Take(5);

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
