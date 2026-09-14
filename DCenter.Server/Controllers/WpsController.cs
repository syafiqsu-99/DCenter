using System.Text;
using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WpsController(WeldReportContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WpsDto>>> GetAll(CancellationToken ct)
        => Ok(await db.WpsItems.OrderBy(w => w.WpsNo)
            .Select(w => new WpsDto(w.Id, w.WpsNo, w.Rev, w.Description, w.IsActive))
            .ToListAsync(ct));

    // Autocomplete for the joint form.
    [HttpGet("search")]
    public async Task<ActionResult<List<WpsDto>>> Search([FromQuery] string? q, CancellationToken ct)
    {
        q = (q ?? string.Empty).Trim();
        var query = db.WpsItems.Where(w => w.IsActive);
        if (q.Length > 0)
            query = query.Where(w => w.WpsNo.Contains(q) || (w.Description != null && w.Description.Contains(q)));
        return Ok(await query.OrderBy(w => w.WpsNo).Take(20)
            .Select(w => new WpsDto(w.Id, w.WpsNo, w.Rev, w.Description, w.IsActive))
            .ToListAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<WpsDto>> Create(WpsUpsert dto, CancellationToken ct)
    {
        var w = new WpsItem { WpsNo = dto.WpsNo, Rev = dto.Rev, Description = dto.Description, IsActive = dto.IsActive };
        db.WpsItems.Add(w);
        await db.SaveChangesAsync(ct);
        return Ok(new WpsDto(w.Id, w.WpsNo, w.Rev, w.Description, w.IsActive));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WpsUpsert dto, CancellationToken ct)
    {
        var w = await db.WpsItems.FindAsync([id], ct);
        if (w is null) return NotFound();
        w.WpsNo = dto.WpsNo;
        w.Rev = dto.Rev;
        w.Description = dto.Description;
        w.IsActive = dto.IsActive;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var w = await db.WpsItems.FindAsync([id], ct);
        if (w is null) return NotFound();
        db.WpsItems.Remove(w);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // GET /api/wps/export -> CSV download.
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await db.WpsItems.OrderBy(w => w.WpsNo).ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("WpsNo,Rev,Description,IsActive");
        foreach (var w in items)
            sb.AppendLine($"{Csv(w.WpsNo)},{Csv(w.Rev)},{Csv(w.Description)},{w.IsActive}");
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "wps.csv");
    }

    // POST /api/wps/import (multipart file) -> upsert by WpsNo. Returns counts.
    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");

        int added = 0, updated = 0, skipped = 0;
        using var reader = new StreamReader(file.OpenReadStream());
        var existing = await db.WpsItems.ToDictionaryAsync(w => w.WpsNo, ct);

        string? line;
        bool header = true;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (header) { header = false; continue; }        // skip header row
            if (string.IsNullOrWhiteSpace(line)) continue;

            var f = ParseCsvLine(line);
            var wpsNo = f.ElementAtOrDefault(0)?.Trim();
            if (string.IsNullOrWhiteSpace(wpsNo)) { skipped++; continue; }

            var rev = f.ElementAtOrDefault(1)?.Trim();
            var desc = f.ElementAtOrDefault(2)?.Trim();
            var active = !bool.TryParse(f.ElementAtOrDefault(3), out var b) || b;

            if (existing.TryGetValue(wpsNo, out var w))
            {
                w.Rev = rev; w.Description = desc; w.IsActive = active;
                updated++;
            }
            else
            {
                var w2 = new WpsItem { WpsNo = wpsNo, Rev = rev, Description = desc, IsActive = active };
                db.WpsItems.Add(w2);
                existing[wpsNo] = w2;
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { added, updated, skipped });
    }

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    // Minimal CSV field parser handling quoted fields with embedded commas/quotes.
    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
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
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { result.Add(sb.ToString()); sb.Clear(); }
                else sb.Append(c);
            }
        }
        result.Add(sb.ToString());
        return result;
    }
}