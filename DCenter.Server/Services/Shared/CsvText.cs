using System.Globalization;
using System.Text;

namespace DCenter.Server.Services;

public static class CsvText
{
    public const long MaxUploadBytes = 2 * 1024 * 1024;
    public const long RequestLimitBytes = MaxUploadBytes + 64 * 1024;

    public sealed record Row(int Line, List<string> Fields);

    public static List<Row> Parse(string text, out string? error)
    {
        error = null;
        var rows = new List<Row>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var rowStart = 1;
        var quoteLine = 1;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            if (fields.Any(f => f.Trim().Length > 0)) rows.Add(new Row(rowStart, fields));
            fields = [];
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    if (c == '\n') line++;
                    field.Append(c);
                }
                continue;
            }

            switch (c)
            {
                case '"' when field.Length == 0:
                    inQuotes = true;
                    quoteLine = line;
                    break;
                case ',':
                    EndField();
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRow();
                    line++;
                    rowStart = line;
                    break;
                default:
                    field.Append(c);
                    break;
            }
        }

        if (inQuotes) error = $"A quoted value starting on line {quoteLine} is never closed. Check for a stray \" in that row.";
        if (field.Length > 0 || fields.Count > 0) EndRow();
        return rows;
    }

    // Maps template columns by header name: letters only, ignoring anything from "(" on, so "Qty (KG)" matches "Qty".
    public static (Dictionary<TCol, int> Columns, List<string> Errors) MapHeader<TCol>(
        List<string> header, IReadOnlyDictionary<string, TCol> aliases, IEnumerable<TCol> required,
        IReadOnlyList<string> headerNames, bool stripKgSuffix = false) where TCol : struct, Enum
    {
        var columns = new Dictionary<TCol, int>();
        var errors = new List<string>();
        for (var i = 0; i < header.Count; i++)
        {
            var name = new string(header[i].Split('(')[0].Where(char.IsLetter).ToArray());
            if (stripKgSuffix && name.EndsWith("kg", StringComparison.OrdinalIgnoreCase)) name = name[..^2];
            if (!aliases.TryGetValue(name, out var col)) continue;
            if (!columns.TryAdd(col, i)) errors.Add($"Column \"{header[i]}\" appears more than once.");
        }
        foreach (var column in required)
            if (!columns.ContainsKey(column))
                errors.Add($"Missing required column \"{headerNames[Convert.ToInt32(column, CultureInfo.InvariantCulture)]}\". Download the template to see the expected columns.");
        return (columns, errors);
    }

    public static byte[] Write(IEnumerable<IEnumerable<string?>> rows)
    {
        var sb = new StringBuilder();
        foreach (var row in rows) sb.Append(string.Join(',', row.Select(Cell))).Append("\r\n");
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Cell(string? value)
    {
        var v = value ?? string.Empty;
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0]) && !decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) v = "'" + v;
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    public static string Decode(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    public static string Unguard(string value)
        => value.Length > 1 && value[0] == '\'' && "=+-@".Contains(value[1]) ? value[1..] : value;
}
