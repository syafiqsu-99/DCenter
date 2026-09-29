using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

// WPS_NO reference table. One row per WPS/P-No pair.
[ApiController]
[Route("api/[controller]")]
public class WpsController(ReferenceTableService<WpsItem> service, IReferenceTable<WpsItem> table) : SettingsControllerBase
{
    private static string?[] Values(WpsUpsert d) => [d.WpsNo, d.PNo, d.BaseMetal, d.Process];

    private static WpsDto ToDto(WpsItem w) => new(w.Id, w.WpsNo, w.BaseMetal, w.Process, w.PNo);

    [HttpGet]
    public async Task<ActionResult<List<WpsDto>>> GetAll(CancellationToken ct)
        => Ok((await service.ListAsync(ct)).Select(ToDto).ToList());

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<WpsDto>> Create(WpsUpsert dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(Values(dto), ct);
        return result.Succeeded ? Ok(ToDto(result.Value!)) : ToAction(result.As<WpsDto>());
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WpsUpsert dto, CancellationToken ct)
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
