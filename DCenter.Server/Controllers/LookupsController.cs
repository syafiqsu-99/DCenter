using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController(WeldReportContext db) : ControllerBase
{
    public static readonly string[] Categories = ["Process", "Size", "Type", "Manuf"];
    private static readonly string[] Headers = ["Category", "Value", "SortOrder", "IsActive"];

    [HttpGet]
    public async Task<ActionResult<List<LookupDto>>> Get([FromQuery] string? category, CancellationToken ct)
    {
        var query = db.Lookups.AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(l => l.Category == category);
        return Ok(await query
            .OrderBy(l => l.Category).ThenBy(l => l.SortOrder).ThenBy(l => l.Value)
            .Select(l => new LookupDto(l.Id, l.Category, l.Value, l.SortOrder, l.IsActive))
            .ToListAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<LookupDto>> Create(LookupUpsert dto, CancellationToken ct)
    {
        var (category, value, error) = Validate(dto);
        if (error is not null) return BadRequest(error);
        if (await IsDuplicateAsync(category, value, null, ct)) return Conflict(DuplicateMessage(category, value));

        var l = new LookupItem { Category = category, Value = value, SortOrder = dto.SortOrder, IsActive = dto.IsActive };
        db.Lookups.Add(l);
        if (!await TrySaveAsync(ct)) return Conflict(DuplicateMessage(category, value));
        return Ok(new LookupDto(l.Id, l.Category, l.Value, l.SortOrder, l.IsActive));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LookupUpsert dto, CancellationToken ct)
    {
        var (category, value, error) = Validate(dto);
        if (error is not null) return BadRequest(error);

        var l = await db.Lookups.FindAsync([id], ct);
        if (l is null) return NotFound();
        if (await IsDuplicateAsync(category, value, id, ct)) return Conflict(DuplicateMessage(category, value));

        l.Category = category;
        l.Value = value;
        l.SortOrder = dto.SortOrder;
        l.IsActive = dto.IsActive;
        if (!await TrySaveAsync(ct)) return Conflict(DuplicateMessage(category, value));
        return NoContent();
    }

    private static (string Category, string Value, string? Error) Validate(LookupUpsert dto)
    {
        var category = (dto.Category ?? string.Empty).Trim();
        var value = (dto.Value ?? string.Empty).Trim();
        if (!Categories.Contains(category))
            return (category, value, $"Category must be one of: {string.Join(", ", Categories)}");
        if (value.Length == 0) return (category, value, "Value is required.");
        if (value.Length > 200) return (category, value, "Value is limited to 200 characters.");
        return (category, value, null);
    }

    private Task<bool> IsDuplicateAsync(string category, string value, int? excludeId, CancellationToken ct)
        => db.Lookups.AnyAsync(l => l.Category == category && l.Value == value && l.Id != (excludeId ?? 0), ct);

    private static string DuplicateMessage(string category, string value)
        => $"'{value}' already exists in {category}.";

    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return false;
        }
    }

    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(List<int> ids, CancellationToken ct)
    {
        var items = await db.Lookups.Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        foreach (var l in items) l.SortOrder = ids.IndexOf(l.Id);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var l = await db.Lookups.FindAsync([id], ct);
        if (l is null) return NotFound();
        db.Lookups.Remove(l);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await db.Lookups
            .OrderBy(l => l.Category).ThenBy(l => l.SortOrder).ThenBy(l => l.Value)
            .ToListAsync(ct);
        var csv = CsvHelper.ToCsv(Headers, items.Select(l =>
            new string?[] { l.Category, l.Value, l.SortOrder.ToString(), l.IsActive ? "1" : "0" }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "dropdown-lists.csv");
    }

    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");

        int added = 0, updated = 0, skipped = 0;
        var existing = await db.Lookups.ToDictionaryAsync(l => (l.Category, l.Value), ct);

        foreach (var f in await CsvHelper.ReadRowsAsync(file, ct))
        {
            var (category, value) = (f.Field(0), f.Field(1));
            if (!Categories.Contains(category) || value.Length == 0) { skipped++; continue; }
            int.TryParse(f.Field(2), out var sortOrder);
            var raw = f.Field(3).ToLowerInvariant();
            var active = raw is "" or "1" or "true" or "yes" or "y";

            if (existing.TryGetValue((category, value), out var l))
            {
                l.SortOrder = sortOrder;
                l.IsActive = active;
                updated++;
            }
            else
            {
                var l2 = new LookupItem { Category = category, Value = value, SortOrder = sortOrder, IsActive = active };
                db.Lookups.Add(l2);
                existing[(category, value)] = l2;
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { added, updated, skipped });
    }
}