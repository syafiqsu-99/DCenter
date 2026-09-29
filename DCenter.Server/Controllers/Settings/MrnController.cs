using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MrnController(WeldReportContext db, ILogger<MrnController> logger) : ControllerBase
{
    private static readonly Column[] Columns =
    [
        new("MRN", Required: true, Key: true),
        new("SpecNo", Required: true, Key: true, "SpecNoRaw", "SpecNo"),
        new("Form", Required: false, Key: false),
        new("FullSpecification", Required: false, Key: false),
    ];

    private static string?[] Values(MrnSpec m) => [m.Mrn, m.SpecNo, m.Form, m.FullSpecification];

    private static string?[] DtoValues(MrnSpecUpsert d) => [d.Mrn, d.SpecNo, d.Form, d.FullSpecification];

    private static void Write(MrnSpec m, string?[] v)
        => (m.Mrn, m.SpecNo, m.Form, m.FullSpecification) = (Clean(v[0]) ?? "", Clean(v[1]) ?? "", Clean(v[2]), Clean(v[3]));

    private static MrnSpecDto ToDto(MrnSpec m) => new(m.Id, m.Mrn, m.SpecNo, m.Form, m.FullSpecification);

    [HttpGet]
    public async Task<ActionResult<List<MrnSpecDto>>> GetAll(CancellationToken ct)
        => Ok(await db.MrnSpecs.AsNoTracking().OrderBy(m => m.Mrn).ThenBy(m => m.SpecNo)
            .Select(m => new MrnSpecDto(m.Id, m.Mrn, m.SpecNo, m.Form, m.FullSpecification))
            .ToListAsync(ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<MrnSpecDto>> Create(MrnSpecUpsert dto, CancellationToken ct)
    {
        if (await ValidateAsync(dto, 0, ct) is { } error) return error;
        var m = new MrnSpec();
        Write(m, DtoValues(dto));
        db.MrnSpecs.Add(m);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(m));
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MrnSpecUpsert dto, CancellationToken ct)
    {
        var m = await db.MrnSpecs.FindAsync([id], ct);
        if (m is null) return NotFound();
        if (await ValidateAsync(dto, id, ct) is { } error) return error;
        Write(m, DtoValues(dto));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var m = await db.MrnSpecs.FindAsync([id], ct);
        if (m is null) return NotFound();
        db.MrnSpecs.Remove(m);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ObjectResult?> ValidateAsync(MrnSpecUpsert dto, int excludeId, CancellationToken ct)
    {
        var (mrn, specNo) = (Clean(dto.Mrn), Clean(dto.SpecNo));
        if (mrn is null || specNo is null) return BadRequest("MRN and Spec No. are required.");

        var key = KeyOf(Columns, DtoValues(dto));
        var candidates = await db.MrnSpecs.AsNoTracking()
            .Where(m => m.Id != excludeId && m.Mrn == mrn && m.SpecNo == specNo)
            .ToListAsync(ct);
        return candidates.Any(m => KeyOf(Columns, Values(m)) == key)
            ? Conflict($"A row with this {KeyLabel(Columns)} already exists. Edit that row instead.")
            : null;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await db.MrnSpecs.AsNoTracking().OrderBy(m => m.Mrn).ThenBy(m => m.SpecNo).ToListAsync(ct);
        return File(ReferenceCsv.Export(Columns, items.Select(Values)), "text/csv; charset=utf-8", "mrn.csv");
    }

    [SupervisorOnly]
    [HttpPost("import")]
    [RequestSizeLimit(CsvText.RequestLimitBytes)]
    public async Task<ActionResult<ImportCounts>> Import(IFormFile? file, CancellationToken ct)
    {
        var sheet = await ReadAsync(file, Columns, ct);
        if (sheet.Errors.Count > 0) return BadRequest(string.Join(" ", sheet.Errors));

        var existing = await db.MrnSpecs.ToListAsync(ct);
        var counts = Upsert(Columns, sheet.Rows, existing, Values, Write, () =>
        {
            var m = new MrnSpec();
            db.MrnSpecs.Add(m);
            return m;
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "MRN import failed to save");
            return Conflict(ImportSaveConflict);
        }
        return Ok(counts);
    }
}
