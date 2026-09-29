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
public class BpvcController(WeldReportContext db, ILogger<BpvcController> logger) : ControllerBase
{
    private static readonly Column[] Columns =
    [
        new("SpecNo", Required: true, Key: true, "SpecNoRaw", "SpecNo"),
        new("Designation", Required: false, Key: true, "Designation", "Grade", "DesignationAlloyGrade"),
        new("UnsNo", Required: false, Key: true),
        new("PNo", Required: true, Key: true),
        new("MinTensile", Required: false, Key: false),
        new("GroupNo", Required: false, Key: false),
        new("IsoGroup", Required: false, Key: false, "IsoGroup", "ISO15608Group"),
        new("BrazingPNo", Required: false, Key: false),
        new("NominalComposition", Required: false, Key: false),
        new("TypicalProductForm", Required: false, Key: false),
        new("NominalThicknessLimits", Required: false, Key: false),
    ];

    private static string?[] Values(BpvcMaterial b) =>
    [
        b.SpecNo, b.Designation, b.UnsNo, b.PNo, b.MinTensile, b.GroupNo, b.IsoGroup,
        b.BrazingPNo, b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits,
    ];

    private static string?[] DtoValues(BpvcMaterialUpsert d) =>
    [
        d.SpecNo, d.Designation, d.UnsNo, d.PNo, d.MinTensile, d.GroupNo, d.IsoGroup,
        d.BrazingPNo, d.NominalComposition, d.TypicalProductForm, d.NominalThicknessLimits,
    ];

    private static void Write(BpvcMaterial b, string?[] v)
    {
        (b.SpecNo, b.Designation, b.UnsNo, b.PNo) = (Clean(v[0]) ?? "", Clean(v[1]), Clean(v[2]), Clean(v[3]) ?? "");
        (b.MinTensile, b.GroupNo, b.IsoGroup, b.BrazingPNo) = (Clean(v[4]), Clean(v[5]), Clean(v[6]), Clean(v[7]));
        (b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits) = (Clean(v[8]), Clean(v[9]), Clean(v[10]));
    }

    private static BpvcMaterialDto ToDto(BpvcMaterial b) => new(
        b.Id, b.SpecNo, b.Designation, b.UnsNo, b.MinTensile, b.PNo,
        b.GroupNo, b.IsoGroup, b.BrazingPNo, b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits);

    private static IQueryable<BpvcMaterial> Ordered(IQueryable<BpvcMaterial> q)
        => q.OrderBy(b => b.SpecNo).ThenBy(b => b.Designation).ThenBy(b => b.UnsNo).ThenBy(b => b.PNo);

    [HttpGet]
    public async Task<ActionResult<List<BpvcMaterialDto>>> GetAll(CancellationToken ct)
        => Ok(await Ordered(db.BpvcMaterials.AsNoTracking()).Select(b => ToDto(b)).ToListAsync(ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<BpvcMaterialDto>> Create(BpvcMaterialUpsert dto, CancellationToken ct)
    {
        if (await ValidateAsync(dto, 0, ct) is { } error) return error;
        var b = new BpvcMaterial();
        Write(b, DtoValues(dto));
        db.BpvcMaterials.Add(b);
        await db.SaveChangesAsync(ct);
        return Ok(ToDto(b));
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, BpvcMaterialUpsert dto, CancellationToken ct)
    {
        var b = await db.BpvcMaterials.FindAsync([id], ct);
        if (b is null) return NotFound();
        if (await ValidateAsync(dto, id, ct) is { } error) return error;
        Write(b, DtoValues(dto));
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var b = await db.BpvcMaterials.FindAsync([id], ct);
        if (b is null) return NotFound();
        db.BpvcMaterials.Remove(b);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<ObjectResult?> ValidateAsync(BpvcMaterialUpsert dto, int excludeId, CancellationToken ct)
    {
        var (specNo, pNo) = (Clean(dto.SpecNo), Clean(dto.PNo));
        if (specNo is null || pNo is null) return BadRequest("Spec No. and P-No. are required.");

        var key = KeyOf(Columns, DtoValues(dto));
        var candidates = await db.BpvcMaterials.AsNoTracking()
            .Where(b => b.Id != excludeId && b.SpecNo == specNo && b.PNo == pNo)
            .ToListAsync(ct);
        return candidates.Any(b => KeyOf(Columns, Values(b)) == key)
            ? Conflict($"A row with this {KeyLabel(Columns)} already exists. Edit that row instead.")
            : null;
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var items = await Ordered(db.BpvcMaterials.AsNoTracking()).ToListAsync(ct);
        return File(ReferenceCsv.Export(Columns, items.Select(Values)), "text/csv; charset=utf-8", "bpvc.csv");
    }

    [SupervisorOnly]
    [HttpPost("import")]
    [RequestSizeLimit(CsvText.MaxUploadBytes + 64 * 1024)]
    public async Task<ActionResult<ImportCounts>> Import(IFormFile? file, CancellationToken ct)
    {
        var sheet = await ReadAsync(file, Columns, ct);
        if (sheet.Errors.Count > 0) return BadRequest(string.Join(" ", sheet.Errors));

        var existing = await db.BpvcMaterials.ToListAsync(ct);
        var counts = Upsert(Columns, sheet.Rows, existing, Values, Write, () =>
        {
            var b = new BpvcMaterial();
            db.BpvcMaterials.Add(b);
            return b;
        });

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "BPVC import failed to save");
            return Conflict("The import could not be saved because the table changed at the same time. Try the import again.");
        }
        return Ok(counts);
    }
}
