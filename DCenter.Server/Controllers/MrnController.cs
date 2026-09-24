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
        .Select(m => new MrnSpecDto(m.Id, m.Mrn, m.Form, m.FullSpecification, m.SpecNo, m.SpecNoRaw))
        .ToListAsync(ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<MrnSpecDto>> Create(MrnSpecUpsert dto, CancellationToken ct)
    {
        var m = new MrnSpec { Mrn = dto.Mrn, Form = dto.Form, FullSpecification = dto.FullSpecification, SpecNo = dto.SpecNo, SpecNoRaw = dto.SpecNoRaw };
        db.MrnSpecs.Add(m);
        await db.SaveChangesAsync(ct);
        return Ok(new MrnSpecDto(m.Id, m.Mrn, m.Form, m.FullSpecification, m.SpecNo, m.SpecNoRaw));
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MrnSpecUpsert dto, CancellationToken ct)
    {
        var m = await db.MrnSpecs.FindAsync([id], ct);
        if (m is null) return NotFound();
        (m.Mrn, m.Form, m.FullSpecification, m.SpecNo, m.SpecNoRaw) = (dto.Mrn, dto.Form, dto.FullSpecification, dto.SpecNo, dto.SpecNoRaw);
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

    private static string RowKey(MrnSpec m) => string.Join('\u001f', new[]
    {
        m.Mrn, m.Form, m.FullSpecification, m.SpecNoRaw, m.SpecNo,
    }.Select(v => v ?? ""));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await db.MrnSpecs.OrderBy(m => m.Mrn).ThenBy(m => m.SpecNo).ToListAsync(ct);
        var csv = CsvText.ToCsv(
            ["MRN", "Form", "FullSpecification", "SpecNoRaw", "SpecNo"],
            items.Select(m => new string?[] { m.Mrn, m.Form, m.FullSpecification, m.SpecNoRaw, m.SpecNo }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "mrn.csv");
    }

    [SupervisorOnly]
    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");

        int added = 0, skipped = 0;
        var seen = (await db.MrnSpecs.ToListAsync(ct)).Select(RowKey).ToHashSet();

        foreach (var f in await CsvText.ReadRowsAsync(file, ct))
        {
            var mrn = f.Field(0);
            var specNo = f.Field(4);
            if (mrn.Length == 0 || specNo.Length == 0) { skipped++; continue; }

            var m = new MrnSpec
            {
                Mrn = mrn,
                Form = f.Field(1),
                FullSpecification = f.Field(2),
                SpecNoRaw = f.Field(3),
                SpecNo = specNo,
            };
            if (!seen.Add(RowKey(m))) { skipped++; continue; }
            db.MrnSpecs.Add(m);
            added++;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { added, updated = 0, skipped });
    }
}