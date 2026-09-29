using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LookupsController(LookupService lookups) : SettingsControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LookupDto>>> Get([FromQuery] string? category, CancellationToken ct)
        => Ok(await lookups.ListAsync(category, ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<LookupDto>> Create(LookupUpsert dto, CancellationToken ct)
        => ToAction(await lookups.CreateAsync(dto, ct));

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LookupUpsert dto, CancellationToken ct)
        => ToNoContent(await lookups.UpdateAsync(id, dto, ct));

    [SupervisorOnly]
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(List<int> ids, CancellationToken ct)
    {
        await lookups.ReorderAsync(ids, ct);
        return NoContent();
    }

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => ToNoContent(await lookups.DeleteAsync(id, ct));

    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
        => File(await lookups.ExportAsync(ct), "text/csv; charset=utf-8", "dropdown-lists.csv");

    [SupervisorOnly]
    [HttpPost("import")]
    [RequestSizeLimit(CsvText.RequestLimitBytes)]
    public async Task<ActionResult<ReferenceCsv.ImportCounts>> Import(IFormFile? file, CancellationToken ct)
        => ToAction(await lookups.ImportAsync(file, ct));
}
