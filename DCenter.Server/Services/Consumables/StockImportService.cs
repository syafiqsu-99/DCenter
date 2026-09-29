using System.Globalization;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Cat = DCenter.Server.Entities.StockCatalog;
using G = DCenter.Server.Services.ConsumableGuards;
using T = DCenter.Server.Services.ConsumableText;

namespace DCenter.Server.Services;

public class StockImportService(
    WeldReportContext db, StoredProcedures sp, ConsumableStore store, ConsumableItemService items, ConsumableLedger ledger,
    TimeProvider time)
{
    public const long MaxFileBytes = CsvText.MaxUploadBytes;
    private const int MaxRows = 5000;

    public const string StatusReady = "Ready";
    public const string StatusNew = "New consumable";
    public const string StatusError = "Error";
    public const string StatusSkipped = "Skipped";
    public const string OpeningReference = "OPENING";

    private static readonly string[] Header =
    [
        "Date", "Type", "Specification", "Diameter", "Brand", "Lot / Heat No.", "Quantity (KG)", "Storage", "Compartment",
        "Holding Oven", "Source", "Remarks",
    ];

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy", "d.M.yyyy"];

    private enum Col { Date, Category, Specification, Diameter, Brand, Lot, Quantity, Storage, Compartment, OvenType, Source, Remarks }

    private static readonly Dictionary<string, Col> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["date"] = Col.Date, ["receiptdate"] = Col.Date, ["receiveddate"] = Col.Date,
        ["type"] = Col.Category, ["category"] = Col.Category, ["consumabletype"] = Col.Category,
        ["specification"] = Col.Specification, ["spec"] = Col.Specification, ["classification"] = Col.Specification,
        ["diameter"] = Col.Diameter, ["dia"] = Col.Diameter, ["size"] = Col.Diameter,
        ["brand"] = Col.Brand, ["electrodebrand"] = Col.Brand, ["manufacturer"] = Col.Brand, ["manuf"] = Col.Brand,
        ["lot"] = Col.Lot, ["lotheatno"] = Col.Lot, ["lotno"] = Col.Lot, ["lotnumber"] = Col.Lot, ["heatno"] = Col.Lot,
        ["lotheatnumber"] = Col.Lot,
        ["quantity"] = Col.Quantity, ["qty"] = Col.Quantity, ["kg"] = Col.Quantity, ["weight"] = Col.Quantity,
        ["storage"] = Col.Storage, ["stage"] = Col.Storage, ["location"] = Col.Storage,
        ["compartment"] = Col.Compartment, ["ovencompartment"] = Col.Compartment, ["bin"] = Col.Compartment,
        ["holdingoven"] = Col.OvenType, ["holdingoventype"] = Col.OvenType, ["oventype"] = Col.OvenType,
        ["source"] = Col.Source, ["origin"] = Col.Source,
        ["remarks"] = Col.Remarks, ["remark"] = Col.Remarks, ["notes"] = Col.Remarks,
    };

    private static readonly Col[] Required = [Col.Category, Col.Specification, Col.Diameter, Col.Brand, Col.Lot, Col.Quantity];

    private static readonly Dictionary<string, (int Id, string OvenType)> Compartments = FixedOvens.Ovens
        .SelectMany(o => Enumerable.Range(1, FixedOvens.CompartmentsPerOven)
            .Select(n => (Code: $"{o.Code}-{n}", Id: FixedOvens.CompartmentId(o.Id, n), o.OvenType)))
        .ToDictionary(c => c.Code, c => (c.Id, c.OvenType), StringComparer.OrdinalIgnoreCase);

    private static readonly string[] CompartmentCodes = [.. Compartments.Keys];

    private sealed record Planned(
        StockImportRowDto Row, string? ItemKey, ItemInput? NewItem, int? ItemId, string Brand, string LotNumber, decimal Qty,
        string Stage, int? CompartmentId, string Source, DateOnly Date, string? Remarks);

    public static byte[] Template(DateOnly today)
        => CsvText.Write(new List<IEnumerable<string?>>
        {
            Header,
            new[]
            {
                today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Cat.ElectrodeFiller, "E7018", "3.20", "Kobelco", "L12345", "5.00",
                Cat.Activated, "AS-1", Cat.OvenAlloySteel, Cat.WeldShop, "EXAMPLE - delete this row",
            },
        });

    public async Task<ServiceResult<StockImportResultDto>> ImportAsync(
        Stream stream, bool commit, bool skipInvalid, string? enteredBy, CancellationToken ct)
    {
        var (user, _, userError) = G.Common(enteredBy, null, time.Today());
        if (userError is not null) return Fail(userError);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        var parsed = CsvText.Parse(CsvText.Decode(buffer.ToArray()), out var parseError);
        if (parseError is not null) return Fail(parseError);
        if (parsed.Count == 0) return Fail("The file is empty.");
        if (parsed.Count - 1 > MaxRows) return Fail($"A file can have at most {MaxRows} rows.");

        var (columns, headerErrors) = MapHeader(parsed[0].Fields);
        if (headerErrors.Count > 0)
            return Ok(new StockImportResultDto(false, parsed.Count - 1, 0, 0, parsed.Count - 1, 0, null, [], headerErrors));

        await using var tx = commit ? await db.Database.BeginTransactionAsync(ct) : null;
        if (commit) await StockLocks.AcquireAsync(db, StockLocks.Master, ct);

        var existing = (await store.ItemsAsync(null, null, ct))
            .ToDictionary(i => Key(i.Specification, i.Diameter));
        var brands = (await store.LookupsAsync(ConsumableItemService.LookupBrand, ct)).Select(l => l.Value)
            .GroupBy(v => v.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var keyById = existing.Values.ToDictionary(i => i.Id, i => Key(i.Specification, i.Diameter));
        var occupants = (await ledger.ActivatedBinsAsync(new LedgerFilter(AnyCompartment: true), ct))
            .Where(b => b.CompartmentId is not null && b.Kg > 0)
            .GroupBy(b => b.CompartmentId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(b => keyById.GetValueOrDefault(b.ItemId, $"#{b.ItemId}")).ToHashSet());
        var newItems = new Dictionary<string, ItemInput>();
        var specifications = await items.SpecificationNamesAsync(ct);
        var imported = (await sp.QueryAsync<OpeningReceipt>("SP_DCenter_Ledger_OpeningReceipts", ct,
                Sql.NVarChar("@ReferenceNo", OpeningReference, 60)))
            .GroupBy(m => OpeningKey(Key(m.Specification, m.Diameter), m.Brand, m.LotNumber, m.QuantityKg, m.ToStage ?? "",
                m.ToCompartmentId, m.TxnDate))
            .ToDictionary(g => g.Key, g => g.First().TxnNo);

        var plan = parsed.Skip(1)
            .Select(r => PlanRow(r, columns, existing, brands, occupants, newItems, specifications, imported))
            .ToList();

        var ready = plan.Count(p => p.Row.Status is StatusReady or StatusNew);
        var rejected = plan.Count(p => p.Row.Status == StatusError);
        var skipped = plan.Count(p => p.Row.Status == StatusSkipped);
        var rows = plan.Select(p => p.Row).ToList();

        if (!commit || (rejected > 0 && !skipInvalid) || ready == 0)
        {
            var fileErrors = new List<string>();
            if (commit && rejected > 0 && !skipInvalid)
                fileErrors.Add("Nothing was imported because some rows are invalid. Fix them, or choose to import only the valid rows.");
            else if (commit && ready == 0)
                fileErrors.Add("There are no valid rows to import.");
            return Ok(new StockImportResultDto(false, plan.Count, ready, newItems.Count, rejected, skipped, null, rows, fileErrors));
        }

        var accepted = plan.Where(p => p.Row.Status is StatusReady or StatusNew).ToList();
        foreach (var itemId in accepted.Where(p => p.ItemId is not null).Select(p => p.ItemId!.Value).Distinct().Order())
            await StockLocks.AcquireAsync(db, StockLocks.Item(itemId), ct);
        foreach (var compartmentId in accepted.Where(p => p.CompartmentId is not null).Select(p => p.CompartmentId!.Value).Distinct().Order())
            await StockLocks.AcquireAsync(db, StockLocks.Compartment(compartmentId), ct);

        var itemsByKey = new Dictionary<string, ConsumableItem>();
        foreach (var key in accepted.Select(p => p.ItemKey!).Distinct())
        {
            if (newItems.TryGetValue(key, out var input))
            {
                var (created, error) = await items.FindOrCreateAsync(input, ct);
                if (created is null) return Fail(error!, StatusCodes.Status409Conflict);
                itemsByKey[key] = created;
            }
            else
            {
                itemsByKey[key] = (await store.ItemAsync(existing[key].Id, ct))!;
            }
        }

        var existingIds = itemsByKey.Values.Where(i => i.Id != 0).Select(i => i.Id).ToList();
        var lots = (await store.LotsAsync(null, existingIds, null, ct))
            .GroupBy(l => (l.ItemId, l.Brand.ToUpperInvariant(), l.LotNumber.ToUpperInvariant()))
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.Id).First().Id);
        var newLots = new Dictionary<(string, string, string), int>();
        var receipts = new List<ConsumableMovement>();

        var txnNo = await ledger.NextTxnNoAsync(ct);
        foreach (var p in accepted)
        {
            var item = itemsByKey[p.ItemKey!];
            var newKey = (p.ItemKey!, p.Brand.ToUpperInvariant(), p.LotNumber.ToUpperInvariant());
            if (!lots.TryGetValue((item.Id, p.Brand.ToUpperInvariant(), p.LotNumber.ToUpperInvariant()), out var lotId)
                && !newLots.TryGetValue(newKey, out lotId))
            {
                var lot = new ConsumableItemLot { ItemId = item.Id, Brand = p.Brand, LotNumber = p.LotNumber };
                await store.InsertLotAsync(lot, ct);
                lotId = lot.Id;
                newLots[newKey] = lotId;
            }

            receipts.Add(new ConsumableMovement
            {
                TxnNo = txnNo,
                TxnType = Cat.TxnReceive,
                TxnDate = p.Date,
                LotId = lotId,
                QuantityKg = p.Qty,
                ToStage = p.Stage,
                ToCompartmentId = p.CompartmentId,
                Source = p.Source,
                Requestor = user,
                ReferenceNo = OpeningReference,
                Remarks = p.Remarks,
                CreatedBy = user,
            });
        }

        await items.EnsureLookupsAsync(
            accepted.SelectMany(p => new[]
            {
                (ConsumableItemService.LookupBrand, p.Brand),
                (ConsumableItemService.LookupSize, itemsByKey[p.ItemKey!].Diameter),
                (ConsumableItemService.LookupType, itemsByKey[p.ItemKey!].Specification),
            }),
            ct);
        await store.AddMovementsAsync(receipts, ct);
        await tx!.CommitAsync(ct);

        return Ok(new StockImportResultDto(true, plan.Count, ready, newItems.Count, rejected, skipped, txnNo, rows, []));
    }

    private Planned PlanRow(
        CsvText.Row r, Dictionary<Col, int> columns, Dictionary<string, ConsumableItem> existing, Dictionary<string, string> brands,
        Dictionary<int, HashSet<string>> occupants, Dictionary<string, ItemInput> newItems,
        IReadOnlyDictionary<string, string> specifications, Dictionary<string, string> imported)
    {
        var messages = new List<string>();
        string? Cell(Col c) => columns.TryGetValue(c, out var i) && i < r.Fields.Count
            ? T.Trimmed(CsvText.Unguard(r.Fields[i].Trim()))
            : null;
        void Note(string? message)
        {
            if (message is not null) messages.Add(message);
        }

        var rawRemarks = Cell(Col.Remarks);
        if (rawRemarks is not null && rawRemarks.StartsWith("EXAMPLE", StringComparison.OrdinalIgnoreCase))
            return Skip(r.Line, $"{T.WarningPrefix} example row from the template — skipped.");

        var date = ParseDate(Cell(Col.Date), time.Today(), messages);

        var rawCategory = Cell(Col.Category);
        var rawSpec = Cell(Col.Specification);
        var rawDiameter = Cell(Col.Diameter);
        var rawOven = Cell(Col.OvenType);
        var (input, itemError) = items.Normalize(new ItemUpsert(rawCategory, rawSpec, rawDiameter, 0m, 0m, null, true, rawOven), specifications);
        string? itemKey = null;
        ConsumableItem? current = null;
        if (input is null) messages.Add(itemError!);
        else
        {
            Note(T.Standardized("Type", rawCategory, input.Category));
            Note(T.Standardized("Specification", rawSpec, input.Specification));
            Note(T.Standardized("Diameter", rawDiameter, input.Diameter));
            Note(T.Standardized("Holding Oven", rawOven, input.HoldingOvenType));
            itemKey = Key(input.Specification, input.Diameter);
            if (existing.TryGetValue(itemKey, out current))
            {
                if (current.Category != input.Category)
                    messages.Add($"{Cat.DiaSpec(input.Diameter, input.Specification)} already exists as {current.Category}.");
                else if (!current.IsActive)
                    messages.Add($"{Cat.DiaSpec(input.Diameter, input.Specification)} is inactive. Reactivate it under Consumables first.");
            }
            else if (newItems.TryGetValue(itemKey, out var earlier))
            {
                input = earlier;
            }
        }

        var brand = ParseBrand(Cell(Col.Brand), brands, messages);
        var lotNumber = ParseLot(Cell(Col.Lot), messages);
        var qty = ParseQuantity(Cell(Col.Quantity), messages);
        var stage = ParseOption(Cell(Col.Storage), "Storage", Cat.ActiveStages, Cat.Normal, messages);
        var source = ParseOption(Cell(Col.Source), "Source", Cat.Sources, Cat.WeldShop, messages);

        int? compartmentId = null;
        string? location = stage;
        var rawCompartment = Cell(Col.Compartment);
        var isElectrode = input?.Category == Cat.ElectrodeFiller;
        if (rawCompartment is not null)
        {
            var (code, suggestion) = T.MatchOption(rawCompartment, CompartmentCodes);
            if (code is null)
                messages.Add($"Compartment \"{rawCompartment}\" is not valid" +
                             (suggestion is null ? "." : $" — did you mean \"{suggestion}\"?") +
                             " Use the oven code and number, e.g. AS-1 … AS-9, MS-1, NI-1, SS-1.");
            else if (input is not null && !isElectrode)
                messages.Add("Bare & powder fillers are not stored in oven compartments. Leave Compartment blank.");
            else if (stage != Cat.Activated)
                messages.Add("A compartment only applies to Activated storage. Set Storage to Activated or leave Compartment blank.");
            else if (input is not null && itemKey is not null)
            {
                Note(T.Standardized("Compartment", rawCompartment, code));
                var (id, ovenType) = Compartments[code];
                var itemOven = current?.HoldingOvenType ?? input.HoldingOvenType;
                if (current is null && input.HoldingOvenType is null)
                {
                    input = input with { HoldingOvenType = ovenType };
                    itemOven = ovenType;
                    messages.Add($"{T.StandardizedPrefix} Holding Oven set to \"{ovenType}\" from compartment {code}.");
                }

                var others = occupants.GetValueOrDefault(id)?.Where(k => k != itemKey).ToList() ?? [];
                if (itemOven is null)
                    messages.Add($"{Cat.DiaSpec(input.Diameter, input.Specification)} has no holding oven type. Set it under Consumables first.");
                else if (itemOven != ovenType)
                    messages.Add($"{Cat.DiaSpec(input.Diameter, input.Specification)} belongs in a {itemOven} oven, not {code} ({ovenType}).");
                else if (others.Count > 0)
                    messages.Add($"{code} already holds another consumable. Choose an empty compartment or one holding the same consumable.");
                else
                {
                    compartmentId = id;
                    location = $"{Cat.Activated} · {code}";
                }
            }
        }
        else if (isElectrode && stage == Cat.Activated)
        {
            location = $"{Cat.Activated} · {Cat.UnassignedBin}";
            messages.Add($"{T.WarningPrefix} no compartment given — the electrodes go to {Cat.UnassignedBin} until placed in an oven.");
        }

        if (itemKey is not null && brand is not null && lotNumber is not null && qty > 0
            && imported.TryGetValue(OpeningKey(itemKey, brand, lotNumber, qty, stage, compartmentId, date), out var earlierTxn))
            messages.Add($"This exact row was already imported as opening stock in {earlierTxn}. Remove it, or change it if it is new stock.");

        var remarks = T.FreeText(rawRemarks, 500);
        var diaSpec = input is not null ? Cat.DiaSpec(input.Diameter, input.Specification) : Cat.DiaSpec(rawDiameter ?? "", rawSpec ?? "");
        var errors = messages.Where(m => !T.IsNote(m)).ToList();
        if (errors.Count > 0 || input is null || itemKey is null || brand is null || lotNumber is null)
            return new Planned(new StockImportRowDto(r.Line, StatusError, diaSpec, brand, lotNumber, qty > 0 ? qty : null, location, messages),
                null, null, null, "", "", 0m, stage, null, source, date, remarks);

        if (compartmentId is int bin)
        {
            if (!occupants.TryGetValue(bin, out var set)) occupants[bin] = set = [];
            set.Add(itemKey);
        }

        var isNew = current is null;
        if (isNew) newItems[itemKey] = input;
        return new Planned(
            new StockImportRowDto(r.Line, isNew ? StatusNew : StatusReady, diaSpec, brand, lotNumber, qty, location, messages),
            itemKey, isNew ? input : null, current?.Id, brand, lotNumber, qty, stage, compartmentId, source, date, remarks);
    }

    // An unparseable date becomes default(DateOnly), as TryParseExact leaves it; the row is rejected by its message.
    internal static DateOnly ParseDate(string? raw, DateOnly today, List<string> messages)
    {
        if (raw is null) return today;
        if (!DateOnly.TryParseExact(raw, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            messages.Add($"Date \"{raw}\" is not valid. Use YYYY-MM-DD (e.g. {today:yyyy-MM-dd}) or DD/MM/YYYY.");
        else if (date > today)
            messages.Add($"Date {date:yyyy-MM-dd} is in the future.");
        return date;
    }

    internal static string? ParseBrand(string? raw, IReadOnlyDictionary<string, string> brands, List<string> messages)
    {
        var brand = raw is null ? null : brands.GetValueOrDefault(T.Collapse(raw)!) ?? T.TitleCase(T.Collapse(raw)!);
        if (brand is null) messages.Add("Brand is required.");
        else if (brand.Length > 100) messages.Add("Brand is limited to 100 characters.");
        else AddNote(messages, T.Standardized("Brand", raw, brand));
        return brand;
    }

    internal static string? ParseLot(string? raw, List<string> messages)
    {
        var lotNumber = T.Collapse(raw)?.ToUpperInvariant();
        if (lotNumber is null) messages.Add("Lot / Heat No. is required.");
        else if (lotNumber.Length > 60) messages.Add("Lot / Heat No. is limited to 60 characters.");
        else AddNote(messages, T.Standardized("Lot / Heat No.", raw, lotNumber));
        return lotNumber;
    }

    internal static decimal ParseQuantity(string? raw, List<string> messages)
    {
        var qty = 0m;
        if (raw is null) messages.Add("Quantity (KG) is required.");
        else if (!decimal.TryParse(raw.Replace("kg", "", StringComparison.OrdinalIgnoreCase).Trim(), NumberStyles.Number,
                     CultureInfo.InvariantCulture, out qty))
            messages.Add($"Quantity \"{raw}\" is not a number. Use kg with a dot for decimals, e.g. 5.00.");
        else
        {
            qty = T.RoundKg(qty);
            if (qty <= 0) messages.Add("Quantity must be greater than 0.");
            else if (qty > T.MaxKg) messages.Add(T.MaxKgError);
        }
        return qty;
    }

    internal static string ParseOption(string? raw, string label, IReadOnlyList<string> options, string fallback, List<string> messages)
    {
        if (raw is null) return fallback;
        var (matched, suggestion) = T.MatchOption(raw, options);
        if (matched is null)
        {
            messages.Add(T.OptionError(label, raw, options, suggestion));
            return fallback;
        }
        AddNote(messages, T.Standardized(label, raw, matched));
        return matched;
    }

    private static void AddNote(List<string> messages, string? message)
    {
        if (message is not null) messages.Add(message);
    }

    private Planned Skip(int line, string message)
        => new(new StockImportRowDto(line, StatusSkipped, null, null, null, null, null, [message]),
            null, null, null, "", "", 0m, Cat.Normal, null, Cat.WeldShop, time.Today(), null);

    private static (Dictionary<Col, int> Columns, List<string> Errors) MapHeader(List<string> header)
        => CsvText.MapHeader(header, Aliases, Required, Header);

    private static string Key(string specification, string diameter) => T.ItemKey(specification, diameter);

    private static string OpeningKey(string itemKey, string brand, string lotNumber, decimal qty, string stage, int? compartmentId, DateOnly date)
        => string.Join('|', itemKey, brand.ToUpperInvariant(), lotNumber.ToUpperInvariant(),
            qty.ToString("0.00", CultureInfo.InvariantCulture), stage, compartmentId?.ToString(CultureInfo.InvariantCulture) ?? "-",
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static ServiceResult<StockImportResultDto> Ok(StockImportResultDto value) => ServiceResult<StockImportResultDto>.Ok(value);

    private static ServiceResult<StockImportResultDto> Fail(string error, int status = StatusCodes.Status400BadRequest)
        => ServiceResult<StockImportResultDto>.Fail(error, status);

    private sealed record OpeningReceipt(
        string TxnNo, DateOnly TxnDate, decimal QuantityKg, string? ToStage, int? ToCompartmentId,
        string Specification, string Diameter, string Brand, string LotNumber);
}
