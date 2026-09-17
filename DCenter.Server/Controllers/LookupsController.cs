using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController(WeldReportContext db) : ControllerBase
{
    public static readonly string[] Categories = ["Process", "Size", "Type", "Manuf"];

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
        if (!Categories.Contains(dto.Category))
            return BadRequest($"Category must be one of: {string.Join(", ", Categories)}");
        var l = new LookupItem
        {
            Category = dto.Category,
            Value = dto.Value,
            SortOrder = dto.SortOrder,
            IsActive = dto.IsActive
        };
        db.Lookups.Add(l);
        await db.SaveChangesAsync(ct);
        return Ok(new LookupDto(l.Id, l.Category, l.Value, l.SortOrder, l.IsActive));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LookupUpsert dto, CancellationToken ct)
    {
        var l = await db.Lookups.FindAsync([id], ct);
        if (l is null) return NotFound();
        l.Category = dto.Category;
        l.Value = dto.Value;
        l.SortOrder = dto.SortOrder;
        l.IsActive = dto.IsActive;
        await db.SaveChangesAsync(ct);
        return NoContent();
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
}