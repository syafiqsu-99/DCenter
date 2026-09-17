using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MrnController(WeldReportContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MrnSpecDto>>> GetAll(CancellationToken ct)
        => Ok(await db.MrnSpecs.OrderBy(m => m.Mrn).ThenBy(m => m.SpecNo)
            .Select(m => new MrnSpecDto(m.Id, m.Mrn, m.Form, m.FullSpecification, m.SpecNo))
            .ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<MrnSpecDto>> Create(MrnSpecUpsert dto, CancellationToken ct)
    {
        var m = new MrnSpec { Mrn = dto.Mrn, Form = dto.Form, FullSpecification = dto.FullSpecification, SpecNo = dto.SpecNo };
        db.MrnSpecs.Add(m);
        await db.SaveChangesAsync(ct);
        return Ok(new MrnSpecDto(m.Id, m.Mrn, m.Form, m.FullSpecification, m.SpecNo));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MrnSpecUpsert dto, CancellationToken ct)
    {
        var m = await db.MrnSpecs.FindAsync([id], ct);
        if (m is null) return NotFound();
        (m.Mrn, m.Form, m.FullSpecification, m.SpecNo) = (dto.Mrn, dto.Form, dto.FullSpecification, dto.SpecNo);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var m = await db.MrnSpecs.FindAsync([id], ct);
        if (m is null) return NotFound();
        db.MrnSpecs.Remove(m);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await db.MrnSpecs.OrderBy(m => m.Mrn).ThenBy(m => m.SpecNo).ToListAsync(ct);
        var csv = CsvHelper.ToCsv(
            ["MRN", "Form", "FullSpecification", "SpecNo"],
            items.Select(m => new string?[] { m.Mrn, m.Form, m.FullSpecification, m.SpecNo }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "mrn.csv");
    }

    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");

        int added = 0, updated = 0, skipped = 0;
        var existing = await db.MrnSpecs.ToDictionaryAsync(m => m.Mrn, ct);

        foreach (var f in await CsvHelper.ReadRowsAsync(file, ct))
        {
            var (mrn, specNo) = (f.Field(0), f.Field(3));
            if (mrn.Length == 0 || specNo.Length == 0) { skipped++; continue; }

            if (existing.TryGetValue(mrn, out var m))
            {
                (m.Form, m.FullSpecification, m.SpecNo) = (f.Field(1), f.Field(2), specNo);
                updated++;
            }
            else
            {
                var m2 = new MrnSpec { Mrn = mrn, Form = f.Field(1), FullSpecification = f.Field(2), SpecNo = specNo };
                db.MrnSpecs.Add(m2);
                existing[mrn] = m2;
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { added, updated, skipped });
    }
}