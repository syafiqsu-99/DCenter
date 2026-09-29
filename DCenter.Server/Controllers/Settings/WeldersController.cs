using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeldersController(WelderService welders) : SettingsControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WelderDto>>> GetAll(CancellationToken ct)
        => Ok(await welders.ListAsync(ct));

    [HttpGet("search")]
    public async Task<ActionResult<List<WelderDto>>> Search([FromQuery] string? q, [FromQuery] bool stockOnly = false, CancellationToken ct = default)
        => Ok(await welders.SearchAsync(q, stockOnly, ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<WelderDto>> Create(WelderDto dto, CancellationToken ct)
        => ToAction(await welders.CreateAsync(dto, ct));

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WelderDto dto, CancellationToken ct)
        => ToNoContent(await welders.UpdateAsync(id, dto, ct));

    [SupervisorOnly]
    [HttpPut("scope")]
    public async Task<ActionResult<int>> SetScope(WelderScopeUpdate dto, CancellationToken ct)
        => ToAction(await welders.SetScopeAsync(dto, ct));

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
        => ToNoContent(await welders.DeleteAsync(id, ct));
}
