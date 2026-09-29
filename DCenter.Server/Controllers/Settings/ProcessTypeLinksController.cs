using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessTypeLinksController(ProcessTypeLinkService links) : SettingsControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProcessTypeLinkDto>>> GetAll(CancellationToken ct)
        => Ok(await links.ListAsync(ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<ProcessTypeLinkDto>> Create(ProcessTypeLinkUpsert dto, CancellationToken ct)
        => ToAction(await links.CreateAsync(dto, ct));

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => ToNoContent(await links.DeleteAsync(id, ct));
}
