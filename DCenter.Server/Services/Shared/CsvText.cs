using System.Globalization;
using System.Text;

namespace DCenter.Server.Services;

public static class CsvText
{
    public sealed record Row(int Line, List<string> Fields);

    public static List<Row> Parse(string text) => Parse(text, out _);

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

    public static async Task<List<string[]>> ReadRowsAsync(IFormFile file, CancellationToken ct)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        return Parse(await reader.ReadToEndAsync(ct)).Skip(1).Select(r => r.Fields.ToArray()).ToList();
    }

    public static string Field(this string[] row, int i) => (row.ElementAtOrDefault(i) ?? "").Trim();

    public static string ToCsv(string[] headers, IEnumerable<string?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', headers));
        foreach (var row in rows) sb.AppendLine(string.Join(',', row.Select(Quote)));
        return sb.ToString();
    }

    private static string Quote(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
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
