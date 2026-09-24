using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

// WPS_NO reference table. One row per WPS/P-No pair.
[ApiController]
[Route("api/[controller]")]
public class WpsController(WeldReportContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WpsDto>>> GetAll(CancellationToken ct)
        => Ok(await db.WpsItems.OrderBy(w => w.WpsNo).ThenBy(w => w.PNo)
            .Select(w => new WpsDto(w.Id, w.WpsNo, w.BaseMetal, w.Process, w.PNo))
            .ToListAsync(ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<WpsDto>> Create(WpsUpsert dto, CancellationToken ct)
    {
        var w = new WpsItem { WpsNo = dto.WpsNo, BaseMetal = dto.BaseMetal, Process = dto.Process, PNo = dto.PNo };
        db.WpsItems.Add(w);
        await db.SaveChangesAsync(ct);
        return Ok(new WpsDto(w.Id, w.WpsNo, w.BaseMetal, w.Process, w.PNo));
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WpsUpsert dto, CancellationToken ct)
    {
        var w = await db.WpsItems.FindAsync([id], ct);
        if (w is null) return NotFound();
        (w.WpsNo, w.BaseMetal, w.Process, w.PNo) = (dto.WpsNo, dto.BaseMetal, dto.Process, dto.PNo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [SupervisorOnly]
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
        var items = await db.WpsItems.OrderBy(w => w.WpsNo).ThenBy(w => w.PNo).ToListAsync(ct);
        var csv = CsvText.ToCsv(
            ["WpsNo", "BaseMetal", "Process", "PNo"],
            items.Select(w => new string?[] { w.WpsNo, w.BaseMetal, w.Process, w.PNo }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "wps.csv");
    }

    // POST /api/wps/import (multipart file, columns: WpsNo,BaseMetal,Process,PNo) -> upsert by (WpsNo, PNo).
    [SupervisorOnly]
    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");

        int added = 0, updated = 0, skipped = 0;
        var existing = await db.WpsItems.ToDictionaryAsync(w => (w.WpsNo, w.PNo), ct);

        foreach (var f in await CsvText.ReadRowsAsync(file, ct))
        {
            var (wpsNo, pNo) = (f.Field(0), f.Field(3));
            if (wpsNo.Length == 0 || pNo.Length == 0) { skipped++; continue; }

            if (existing.TryGetValue((wpsNo, pNo), out var w))
            {
                (w.BaseMetal, w.Process) = (f.Field(1), f.Field(2));
                updated++;
            }
            else
            {
                var w2 = new WpsItem { WpsNo = wpsNo, BaseMetal = f.Field(1), Process = f.Field(2), PNo = pNo };
                db.WpsItems.Add(w2);
                existing[(wpsNo, pNo)] = w2;
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { added, updated, skipped });
    }
}