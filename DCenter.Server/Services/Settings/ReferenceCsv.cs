namespace DCenter.Server.Services;

public static class ReferenceCsv
{
    public sealed record Column(string Header, bool Required, bool Key, params string[] Aliases)
    {
        public IEnumerable<string> Names => Aliases.Length > 0 ? Aliases : [Header];
    }

    public sealed record Sheet(List<string?[]> Rows, List<string> Errors);

    public sealed record ImportCounts(int Added, int Updated, int Unchanged, int Skipped);

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static byte[] Export(IReadOnlyList<Column> columns, IEnumerable<string?[]> rows)
        => CsvText.Write([columns.Select(c => c.Header), .. rows]);

    public static string Key(params string?[] parts) => string.Join('\u001f', parts.Select(Normalize));

    public static string Normalize(string? value)
        => string.Join(' ', (value ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    public static string KeyOf(IReadOnlyList<Column> columns, string?[] values)
        => Key([.. columns.Select((c, i) => (c, i)).Where(x => x.c.Key).Select(x => values[x.i])]);

    public static string KeyLabel(IReadOnlyList<Column> columns)
        => string.Join(" / ", columns.Where(c => c.Key).Select(c => c.Header));

    private static string HeaderName(string header)
        => new string(CsvText.Unguard(header.Trim().TrimStart('\uFEFF')).Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

    public static (int[][] Map, List<string> Errors) MapHeader(IReadOnlyList<string> header, IReadOnlyList<Column> columns)
    {
        var names = header.Select(HeaderName).ToList();
        var errors = new List<string>();
        var map = new int[columns.Count][];

        for (var c = 0; c < columns.Count; c++)
        {
            var found = new List<int>();
            foreach (var name in columns[c].Names.Select(HeaderName))
            {
                var hits = names.Select((n, i) => (n, i)).Where(x => x.n == name).Select(x => x.i).ToList();
                if (hits.Count > 1) errors.Add($"Column \"{header[hits[1]].Trim()}\" appears more than once. Remove the extra column and import again.");
                if (hits.Count > 0) found.Add(hits[0]);
            }
            if (found.Count == 0 && columns[c].Required)
                errors.Add($"Missing required column \"{columns[c].Header}\". Export the table to see the expected columns.");
            map[c] = [.. found];
        }
        return (map, errors);
    }

    public static Sheet Read(string text, IReadOnlyList<Column> columns)
    {
        var parsed = CsvText.Parse(text, out var parseError);
        if (parseError is not null) return new Sheet([], [parseError]);
        if (parsed.Count == 0) return new Sheet([], ["The file is empty. Export the table to get a file with the expected columns."]);

        var (map, errors) = MapHeader(parsed[0].Fields, columns);
        if (errors.Count > 0) return new Sheet([], errors);

        var rows = parsed.Skip(1).Select(r => map.Select(candidates => Cell(r.Fields, candidates)).ToArray()).ToList();
        return new Sheet(rows, []);
    }

    public static async Task<Sheet> ReadAsync(IFormFile? file, IReadOnlyList<Column> columns, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return new Sheet([], ["No file uploaded. Choose a CSV file and try again."]);
        if (file.Length > CsvText.MaxUploadBytes) return new Sheet([], ["The file is larger than 2 MB. Split it into smaller files and import each one."]);

        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        return Read(CsvText.Decode(buffer.ToArray()), columns);
    }

    private static string? Cell(List<string> fields, int[] candidates)
    {
        foreach (var i in candidates)
        {
            var value = i < fields.Count ? CsvText.Unguard(fields[i].Trim()) : "";
            if (value.Length > 0) return value;
        }
        return null;
    }

    public static ImportCounts Upsert<T>(
        IReadOnlyList<Column> columns,
        IEnumerable<string?[]> rows,
        IEnumerable<T> existing,
        Func<T, string?[]> read,
        Action<T, string?[]> write,
        Func<T> create) where T : class
    {
        int added = 0, updated = 0, unchanged = 0, skipped = 0;
        var byKey = new Dictionary<string, T>();
        foreach (var e in existing) byKey.TryAdd(KeyOf(columns, read(e)), e);

        foreach (var values in rows)
        {
            if (columns.Select((c, i) => (c, i)).Any(x => x.c.Required && string.IsNullOrEmpty(values[x.i])))
            {
                skipped++;
                continue;
            }

            var key = KeyOf(columns, values);
            if (byKey.TryGetValue(key, out var match))
            {
                if (read(match).SequenceEqual(values, SameText)) unchanged++;
                else
                {
                    write(match, values);
                    updated++;
                }
                continue;
            }

            var item = create();
            write(item, values);
            byKey[key] = item;
            added++;
        }

        return new ImportCounts(added, updated, unchanged, skipped);
    }

    private static readonly TrimmedTextComparer SameText = new();

    private sealed class TrimmedTextComparer : IEqualityComparer<string?>
    {
        public bool Equals(string? x, string? y) => string.Equals((x ?? "").Trim(), (y ?? "").Trim(), StringComparison.Ordinal);
        public int GetHashCode(string? obj) => StringComparer.Ordinal.GetHashCode((obj ?? "").Trim());
    }
}
