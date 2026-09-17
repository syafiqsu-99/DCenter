using System.Text;

namespace DCenter.Server.Services;

public static class CsvHelper
{
    public static async Task<List<string[]>> ReadRowsAsync(IFormFile file, CancellationToken ct)
    {
        var rows = new List<string[]>();
        using var reader = new StreamReader(file.OpenReadStream());
        var header = true;
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (header) { header = false; continue; }
            if (string.IsNullOrWhiteSpace(line)) continue;
            rows.Add(ParseLine(line));
        }
        return rows;
    }

    public static string ToCsv(string[] headers, IEnumerable<string?[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', headers));
        foreach (var row in rows)
            sb.AppendLine(string.Join(',', row.Select(Escape)));
        return sb.ToString();
    }

    public static string Field(this string[] row, int i) => (row.ElementAtOrDefault(i) ?? "").Trim();

    private static string Escape(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        return v.Contains(',') || v.Contains('"') || v.Contains('\n')
            ? $"\"{v.Replace("\"", "\"\"")}\""
            : v;
    }

    private static string[] ParseLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') inQuotes = true;
            else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }
        result.Add(sb.ToString());
        return [.. result];
    }
}