using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MrnController(ReferenceTableService<MrnSpec> service, IReferenceTable<MrnSpec> table) : SettingsControllerBase
{
    private static string?[] Values(MrnSpecUpsert d) => [d.Mrn, d.SpecNo, d.Form, d.FullSpecification];

    private static MrnSpecDto ToDto(MrnSpec m) => new(m.Id, m.Mrn, m.SpecNo, m.Form, m.FullSpecification);

    [HttpGet]
    public async Task<ActionResult<List<MrnSpecDto>>> GetAll(CancellationToken ct)
        => Ok((await service.ListAsync(ct)).Select(ToDto).ToList());

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<MrnSpecDto>> Create(MrnSpecUpsert dto, CancellationToken ct)
    {
        var result = await service.CreateAsync(Values(dto), ct);
        return result.Succeeded ? Ok(ToDto(result.Value!)) : ToAction(result.As<MrnSpecDto>());
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MrnSpecUpsert dto, CancellationToken ct)
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
