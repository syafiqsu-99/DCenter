using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Controllers;

// WPS_NO reference table. One row per WPS/P-No pair.
[ApiController]
[Route("api/[controller]")]
public class WpsController(WeldReportContext db, ILogger<WpsController> logger) : ControllerBase
{
    private static readonly Column[] Columns =
    [
        new("WpsNo", Required: true, Key: true),
        new("PNo", Required: true, Key: true),
        new("BaseMetal", Required: false, Key: false),
        new("Process", Required: false, Key: false),
    ];

    private static string?[] Values(WpsItem w) => [w.WpsNo, w.PNo, w.BaseMetal, w.Process];

    private static void Write(WpsItem w, string?[] v)
        => (w.WpsNo, w.PNo, w.BaseMetal, w.Process) = (Clean(v[0]) ?? "", Clean(v[1]) ?? "", Clean(v[2]), Clean(v[3]));

    [HttpGet]
    public async Task<ActionResult<List<WpsDto>>> GetAll(CancellationToken ct)
        => Ok(await db.WpsItems.AsNoTracking().OrderBy(w => w.WpsNo).ThenBy(w => w.PNo)
            .Select(w => new WpsDto(w.Id, w.WpsNo, w.BaseMetal, w.Process, w.PNo))
            .ToListAsync(ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<WpsDto>> Create(WpsUpsert dto, CancellationToken ct)
    {
        if (await ValidateAsync(dto, 0, ct) is { } error) return error;
        var w = new WpsItem();
        Write(w, [dto.WpsNo, dto.PNo, dto.BaseMetal, dto.Process]);
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
        if (await ValidateAsync(dto, id, ct) is { } error) return error;
        Write(w, [dto.WpsNo, dto.PNo, dto.BaseMetal, dto.Process]);
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

    private async Task<ObjectResult?> ValidateAsync(WpsUpsert dto, int excludeId, CancellationToken ct)
    {
        var (wpsNo, pNo) = (Clean(dto.WpsNo), Clean(dto.PNo));
        if (wpsNo is null || pNo is null) return BadRequest("WPS No. and P-No. are required.");
        return await db.WpsItems.AnyAsync(w => w.Id != excludeId && w.WpsNo == wpsNo && w.PNo == pNo, ct)
            ? Conflict($"A row with this {KeyLabel(Columns)} already exists. Edit that row instead.")
            : null;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await db.WpsItems.AsNoTracking().OrderBy(w => w.WpsNo).ThenBy(w => w.PNo).ToListAsync(ct);
        return File(ReferenceCsv.Export(Columns, items.Select(Values)), "text/csv; charset=utf-8", "wps.csv");
    }

    [SupervisorOnly]
    [HttpPost("import")]
    [RequestSizeLimit(CsvText.MaxUploadBytes + 64 * 1024)]
    public async Task<ActionResult<ImportCounts>> Import(IFormFile? file, CancellationToken ct)
    {
        var sheet = await ReadAsync(file, Columns, ct);
        if (sheet.Errors.Count > 0) return BadRequest(string.Join(" ", sheet.Errors));

        var existing = await db.WpsItems.ToListAsync(ct);
        var counts = Upsert(Columns, sheet.Rows, existing, Values, Write, () =>
        {
            var w = new WpsItem();
            db.WpsItems.Add(w);
            return w;
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "WPS import failed to save");
            return Conflict("The import could not be saved because the table changed at the same time. Try the import again.");
        }
        return Ok(counts);
    }
}
