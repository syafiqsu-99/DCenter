using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BpvcController(WeldReportContext db) : ControllerBase
{
    private static BpvcMaterialDto ToDto(BpvcMaterial b) => new(
        b.Id, b.SpecNo, b.Designation, b.UnsNo, b.MinTensile, b.PNo,
        b.GroupNo, b.IsoGroup, b.BrazingPNo, b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits);

    private static void Apply(BpvcMaterial b, BpvcMaterialUpsert dto)
    {
        (b.SpecNo, b.Designation, b.UnsNo, b.MinTensile, b.PNo) = (dto.SpecNo, dto.Designation, dto.UnsNo, dto.MinTensile, dto.PNo);
        (b.GroupNo, b.IsoGroup, b.BrazingPNo) = (dto.GroupNo, dto.IsoGroup, dto.BrazingPNo);
        (b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits) =
            (dto.NominalComposition, dto.TypicalProductForm, dto.NominalThicknessLimits);
    }

    [HttpGet]
    public async Task<ActionResult<List<BpvcMaterialDto>>> GetAll(CancellationToken ct)
        => Ok(await db.BpvcMaterials.OrderBy(b => b.SpecNo).ThenBy(b => b.PNo)
            .Select(b => ToDto(b)).ToListAsync(ct));

    [HttpPost]
    public async Task<ActionResult<BpvcMaterialDto>> Create(BpvcMaterialUpsert dto, CancellationToken ct)
    {
        var b = new BpvcMaterial();
        Apply(b, dto);
        db.BpvcMaterials.Add(b);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(b));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, BpvcMaterialUpsert dto, CancellationToken ct)
    {
        var b = await db.BpvcMaterials.FindAsync([id], ct);
        if (b is null) return NotFound();
        Apply(b, dto);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var b = await db.BpvcMaterials.FindAsync([id], ct);
        if (b is null) return NotFound();
        db.BpvcMaterials.Remove(b);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static readonly string[] Headers =
    [
        "SpecNo", "Designation", "UnsNo", "MinTensile", "PNo", "GroupNo",
        "IsoGroup", "BrazingPNo", "NominalComposition", "TypicalProductForm", "NominalThicknessLimits",
    ];

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await db.BpvcMaterials.OrderBy(b => b.SpecNo).ThenBy(b => b.PNo).ToListAsync(ct);
        var csv = CsvHelper.ToCsv(Headers, items.Select(b => new string?[]
        {
            b.SpecNo, b.Designation, b.UnsNo, b.MinTensile, b.PNo,
            b.GroupNo, b.IsoGroup, b.BrazingPNo, b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits,
        }));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "bpvc.csv");
    }

    [HttpPost("import")]
    public async Task<ActionResult<object>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");

        int added = 0, updated = 0, skipped = 0;
        var existing = await db.BpvcMaterials.ToDictionaryAsync(b => (b.SpecNo, b.PNo), ct);

        foreach (var f in await CsvHelper.ReadRowsAsync(file, ct))
        {
            var (specNo, pNo) = (f.Field(0), f.Field(4));
            if (specNo.Length == 0 || pNo.Length == 0) { skipped++; continue; }

            var dto = new BpvcMaterialUpsert(specNo, f.Field(1), f.Field(2), f.Field(3), pNo,
                f.Field(5), f.Field(6), f.Field(7), f.Field(8), f.Field(9), f.Field(10));

            if (existing.TryGetValue((specNo, pNo), out var b))
            {
                Apply(b, dto);
                updated++;
            }
            else
            {
                var b2 = new BpvcMaterial();
                Apply(b2, dto);
                db.BpvcMaterials.Add(b2);
                existing[(specNo, pNo)] = b2;
                added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { added, updated, skipped });
    }
}