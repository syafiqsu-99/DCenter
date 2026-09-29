using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BpvcController(ReferenceTableService<BpvcMaterial> service, IReferenceTable<BpvcMaterial> table) : SettingsControllerBase
{
    private static string?[] Values(BpvcMaterialUpsert d) =>
    [
        d.SpecNo, d.Designation, d.UnsNo, d.PNo, d.MinTensile, d.GroupNo, d.IsoGroup,
        d.BrazingPNo, d.NominalComposition, d.TypicalProductForm, d.NominalThicknessLimits,
    ];

    private static BpvcMaterialDto ToDto(BpvcMaterial b) => new(
        b.Id, b.SpecNo, b.Designation, b.UnsNo, b.MinTensile, b.PNo,
        b.GroupNo, b.IsoGroup, b.BrazingPNo, b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits);

    [HttpGet]
    public async Task<ActionResult<List<BpvcMaterialDto>>> GetAll(CancellationToken ct)
        => Ok((await service.ListAsync(ct)).Select(ToDto).ToList());

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<BpvcMaterialDto>> Create(BpvcMaterialUpsert dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(Values(dto), ct);
        return result.Succeeded ? Ok(ToDto(result.Value!)) : ToAction(result.As<BpvcMaterialDto>());
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, BpvcMaterialUpsert dto, CancellationToken ct)
        => ToNoContent(await service.UpdateAsync(id, Values(dto), ct));

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => ToNoContent(await service.DeleteAsync(id, ct));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
        => File(await service.ExportAsync(ct), "text/csv; charset=utf-8", table.FileName);

    [SupervisorOnly]
    [HttpPost("import")]
    [RequestSizeLimit(CsvText.RequestLimitBytes)]
    public async Task<ActionResult<ReferenceCsv.ImportCounts>> Import(IFormFile? file, CancellationToken ct)
        => ToAction(await service.ImportAsync(file, ct));
}
