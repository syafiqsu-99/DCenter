using System.Globalization;
using System.Text;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.EntityFrameworkCore;
using Cat = DCenter.Server.Entities.StockCatalog;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public class ConsumableImportService(WeldReportContext db, ConsumableItemService items, ConsumableLedger ledger)
{
    public const long MaxFileBytes = 2 * 1024 * 1024;
    private const int MaxRows = 5000;

    public const string ActionCreate = "Create";
    public const string ActionUpdate = "Update";
    public const string ActionUnchanged = "Unchanged";
    public const string ActionError = "Error";

    private static readonly string[] Header =
    [
        "Type", "Specification", "Diameter", "Min Stock (KG)", "Activated Min (KG)", "Finish Threshold (KG)",
        "Holding Oven Type", "Active",
    ];

    private enum Col { Category, Specification, Diameter, MinStock, ActivatedMin, FinishThreshold, OvenType, Active }

    private static readonly Dictionary<string, Col> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["type"] = Col.Category, ["category"] = Col.Category, ["consumabletype"] = Col.Category,
        ["specification"] = Col.Specification, ["spec"] = Col.Specification, ["classification"] = Col.Specification,
        ["diameter"] = Col.Diameter, ["dia"] = Col.Diameter, ["size"] = Col.Diameter,
        ["minstock"] = Col.MinStock, ["minimumstock"] = Col.MinStock,
        ["activatedmin"] = Col.ActivatedMin, ["activatedminimum"] = Col.ActivatedMin,
        ["finishthreshold"] = Col.FinishThreshold,
        ["holdingoventype"] = Col.OvenType, ["holdingoven"] = Col.OvenType, ["oventype"] = Col.OvenType,
        ["active"] = Col.Active, ["isactive"] = Col.Active,
    };

    private sealed record Existing(ConsumableItem Item, bool HasStock, bool InOven);

    private sealed record Planned(int Line, string Action, ItemInput? Input, Existing? Target, ImportRowDto Row);

    public async Task<byte[]> ExportAsync(bool templateOnly, CancellationToken ct)
    {
        if (templateOnly) return CsvText.Write([Header]);

        var rows = await db.ConsumableItems.AsNoTracking()
            .OrderBy(i => i.Category).ThenBy(i => i.Specification).ThenBy(i => i.Diameter)
            .Select(i => new { i.Category, i.Specification, i.Diameter, i.MinStockKg, i.ActivatedMinKg, i.FinishThresholdKg, i.HoldingOvenType, i.IsActive })
            .ToListAsync(ct);

        var lines = new List<IEnumerable<string?>> { Header };
        lines.AddRange(rows
            .OrderBy(i => i.Category).ThenBy(i => i.Specification).ThenBy(i => i.Diameter)
            .Select(i => new[]
            {
                i.Category, i.Specification, T.FormatDiameter(i.Diameter), Num(i.MinStockKg), Num(i.ActivatedMinKg),
                i.FinishThresholdKg is decimal f ? Num(f) : string.Empty, i.HoldingOvenType ?? string.Empty, i.IsActive ? "Yes" : "No",
            }));
        return CsvText.Write(lines);
    }

    public async Task<ServiceResult<ImportResultDto>> ImportAsync(Stream stream, bool commit, bool skipInvalid, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var text = Decode(buffer.ToArray());

        var parsed = CsvText.Parse(text, out var parseError);
        if (parseError is not null) return Fail(parseError);
        if (parsed.Count == 0) return Fail("The file is empty.");
        if (parsed.Count - 1 > MaxRows) return Fail($"A file can have at most {MaxRows} rows.");

        var (columns, headerErrors) = MapHeader(parsed[0].Fields);
        if (headerErrors.Count > 0)
            return ServiceResult<ImportResultDto>.Ok(new ImportResultDto(false, parsed.Count - 1, 0, 0, 0, parsed.Count - 1, [], headerErrors));

        await using var tx = commit ? await db.Database.BeginTransactionAsync(ct) : null;
        if (commit) await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        var stockedIds = await db.ConsumableItemLots.AsNoTracking()
            .Where(l => l.Movements.Any())
            .Select(l => l.ItemId)
            .Distinct()
            .ToListAsync(ct);
        var stocked = stockedIds.ToHashSet();
        var inOven = (await ledger.ActivatedBinsAsync(null, ct))
            .Where(b => b.CompartmentId is not null && b.Kg > 0)
            .Select(b => b.ItemId)
            .ToHashSet();
        var existing = (commit ? await db.ConsumableItems.ToListAsync(ct) : await db.ConsumableItems.AsNoTracking().ToListAsync(ct))
            .ToDictionary(i => Key(i.Specification, i.Diameter), i => new Existing(i, stocked.Contains(i.Id), inOven.Contains(i.Id)));

        var seen = new Dictionary<string, int>();
        var plan = parsed.Skip(1).Select(r => PlanRow(r, columns, existing, seen)).ToList();

        var created = plan.Count(p => p.Action == ActionCreate);
        var updated = plan.Count(p => p.Action == ActionUpdate);
        var unchanged = plan.Count(p => p.Action == ActionUnchanged);
        var rejected = plan.Count(p => p.Action == ActionError);
        var rows = plan.Select(p => p.Row).ToList();

        if (!commit || (rejected > 0 && !skipInvalid) || created + updated == 0)
        {
            var fileErrors = commit && rejected > 0 && !skipInvalid
                ? new List<string> { "Nothing was imported because some rows are invalid. Fix them, or choose to import only the valid rows." }
                : [];
            return ServiceResult<ImportResultDto>.Ok(new ImportResultDto(false, plan.Count, created, updated, unchanged, rejected, rows, fileErrors));
        }

        foreach (var p in plan.Where(p => p.Action is ActionCreate or ActionUpdate))
        {
            var n = p.Input!;
            var item = p.Target?.Item ?? db.ConsumableItems.Add(new ConsumableItem()).Entity;
            item.Category = n.Category;
            item.Specification = n.Specification;
            item.Diameter = n.Diameter;
            item.MinStockKg = n.MinStockKg;
            item.ActivatedMinKg = n.ActivatedMinKg;
            item.FinishThresholdKg = n.FinishThresholdKg;
            item.HoldingOvenType = n.HoldingOvenType;
            item.IsActive = n.IsActive;
        }

        var applied = plan.Where(p => p.Input is not null && p.Action is ActionCreate or ActionUpdate).Select(p => p.Input!).ToList();
        await items.EnsureLookupsAsync(
            applied.SelectMany(n => new[] { (ConsumableItemService.LookupSize, T.FormatDiameter(n.Diameter)), (ConsumableItemService.LookupType, n.Specification) }),
            ct);
        await db.SaveChangesAsync(ct);
        await tx!.CommitAsync(ct);

        return ServiceResult<ImportResultDto>.Ok(new ImportResultDto(true, plan.Count, created, updated, unchanged, rejected, rows, []));
    }

    private Planned PlanRow(CsvText.Row r, Dictionary<Col, int> columns, Dictionary<string, Existing> existing, Dictionary<string, int> seen)
    {
        var messages = new List<string>();
        string? Cell(Col c) => columns.TryGetValue(c, out var i) && i < r.Fields.Count ? Unguard(r.Fields[i].Trim()) : null;
        bool Has(Col c) => columns.ContainsKey(c);

        var rawCategory = Cell(Col.Category);
        var rawSpec = Cell(Col.Specification);
        var rawDiameter = Cell(Col.Diameter);
        var provisional = T.Specification(rawSpec);
        var provisionalDia = T.Diameter(rawDiameter);
        existing.TryGetValue(Key(provisional ?? string.Empty, provisionalDia ?? 0m), out var match);
        var current = match?.Item;

        decimal? Number(Col c, string label, decimal? fallback, bool blankIsNull = false)
        {
            if (!Has(c)) return fallback;
            var raw = Cell(c);
            if (string.IsNullOrEmpty(raw)) return blankIsNull ? null : current is null ? 0m : fallback;
            if (decimal.TryParse(raw.Replace("kg", "", StringComparison.OrdinalIgnoreCase).Trim(), NumberStyles.Number,
                    CultureInfo.InvariantCulture, out var v))
                return v;
            messages.Add($"{label} \"{raw}\" is not a number.");
            return fallback;
        }

        var min = Number(Col.MinStock, "Min Stock", current?.MinStockKg ?? 0m) ?? 0m;
        var activatedMin = Number(Col.ActivatedMin, "Activated Min", current?.ActivatedMinKg ?? 0m) ?? 0m;
        var finish = Number(Col.FinishThreshold, "Finish Threshold", current?.FinishThresholdKg, blankIsNull: true);
        var ovenType = Has(Col.OvenType) ? NullIfEmpty(Cell(Col.OvenType)) : current?.HoldingOvenType;

        var isActive = current?.IsActive ?? true;
        if (Has(Col.Active) && !string.IsNullOrEmpty(Cell(Col.Active)))
        {
            var parsedActive = ParseBool(Cell(Col.Active)!);
            if (parsedActive is null) messages.Add($"Active \"{Cell(Col.Active)}\" must be Yes or No.");
            else isActive = parsedActive.Value;
        }

        if (min < 0 || activatedMin < 0 || finish < 0) messages.Add("Quantities cannot be negative.");

        ItemInput? input = null;
        if (messages.Count == 0)
        {
            var (n, error) = items.Normalize(new ItemUpsert(rawCategory, rawSpec, rawDiameter, min, activatedMin, finish, isActive, ovenType));
            if (n is null) messages.Add(error!);
            else input = n;
        }

        if (input is not null && input.Category == Cat.ElectrodeFiller && input.HoldingOvenType is null)
            messages.Add("Warning: no holding oven type — electrodes cannot be placed in an oven until it is set.");

        if (input is not null)
        {
            var key = Key(input.Specification, input.Diameter);
            if (seen.TryGetValue(key, out var firstLine))
            {
                messages.Insert(0, $"Duplicate of line {firstLine} ({Cat.DiaSpec(input.Diameter, input.Specification)}).");
                input = null;
            }
            else
            {
                seen[key] = r.Line;
                existing.TryGetValue(key, out match);
            }
        }

        var errors = messages.Where(m => !m.StartsWith("Warning:", StringComparison.Ordinal)).ToList();
        var diaSpec = input is not null ? Cat.DiaSpec(input.Diameter, input.Specification) : Cat.DiaSpec(rawDiameter ?? "", rawSpec ?? "");
        if (input is null || errors.Count > 0)
            return new Planned(r.Line, ActionError, null, null, new ImportRowDto(r.Line, ActionError, rawCategory, diaSpec, messages));

        if (match is null)
            return new Planned(r.Line, ActionCreate, input, null, new ImportRowDto(r.Line, ActionCreate, input.Category, diaSpec, messages));

        var item = match.Item;
        if (item.Category != input.Category && match.HasStock)
        {
            messages.Insert(0, $"Already exists as {item.Category}; the type cannot change once stock has been recorded.");
            return new Planned(r.Line, ActionError, null, null, new ImportRowDto(r.Line, ActionError, input.Category, diaSpec, messages));
        }
        if (match.InOven && item.HoldingOvenType != input.HoldingOvenType)
        {
            messages.Insert(0, $"Holding oven type cannot change from {item.HoldingOvenType ?? "none"} while its electrodes are in oven " +
                               "compartments. Move or finish them first.");
            return new Planned(r.Line, ActionError, null, null, new ImportRowDto(r.Line, ActionError, input.Category, diaSpec, messages));
        }

        var changes = new List<string>();
        void Compare<TValue>(string label, TValue before, TValue after)
        {
            if (!EqualityComparer<TValue>.Default.Equals(before, after)) changes.Add($"{label}: {Show(before)} → {Show(after)}");
        }
        Compare("Type", item.Category, input.Category);
        Compare("Min Stock", item.MinStockKg, input.MinStockKg);
        Compare("Activated Min", item.ActivatedMinKg, input.ActivatedMinKg);
        Compare("Finish Threshold", item.FinishThresholdKg, input.FinishThresholdKg);
        Compare("Holding Oven", item.HoldingOvenType, input.HoldingOvenType);
        Compare("Active", item.IsActive, input.IsActive);

        var action = changes.Count > 0 ? ActionUpdate : ActionUnchanged;
        return new Planned(r.Line, action, input, match, new ImportRowDto(r.Line, action, input.Category, diaSpec, [.. changes, .. messages]));
    }

    private static (Dictionary<Col, int> Columns, List<string> Errors) MapHeader(List<string> header)
    {
        var columns = new Dictionary<Col, int>();
        var errors = new List<string>();
        for (var i = 0; i < header.Count; i++)
        {
            var name = new string(header[i].Split('(')[0].Where(char.IsLetter).ToArray());
            if (name.EndsWith("kg", StringComparison.OrdinalIgnoreCase)) name = name[..^2];
            if (!Aliases.TryGetValue(name, out var col)) continue;
            if (!columns.TryAdd(col, i)) errors.Add($"Column \"{header[i]}\" appears more than once.");
        }
        foreach (var required in new[] { Col.Category, Col.Specification, Col.Diameter })
            if (!columns.ContainsKey(required))
                errors.Add($"Missing required column \"{Header[(int)required]}\". Download the template to see the expected columns.");
        return (columns, errors);
    }

    private static string Key(string specification, decimal diameter) => $"{specification}|{T.FormatDiameter(diameter)}".ToUpperInvariant();

    private static string Num(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool? ParseBool(string raw) => raw.Trim().ToLowerInvariant() switch
    {
        "yes" or "y" or "true" or "1" or "active" => true,
        "no" or "n" or "false" or "0" or "inactive" => false,
        _ => null,
    };

    private static string Show<TValue>(TValue value) => value switch
    {
        null => "—",
        decimal d => Num(d),
        bool b => b ? "Yes" : "No",
        _ => value.ToString() ?? "—",
    };

    private static ServiceResult<ImportResultDto> Fail(string error) => ServiceResult<ImportResultDto>.Fail(error);

    private static string Decode(byte[] bytes)
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

    private static string Unguard(string value)
        => value.Length > 1 && value[0] == '\'' && "=+-@".Contains(value[1]) ? value[1..] : value;
}
