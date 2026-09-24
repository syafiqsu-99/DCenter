using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProcessTypeLinksController(WeldReportContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ProcessTypeLinkDto>>> GetAll(CancellationToken ct)
        => Ok(await db.ProcessTypeLinks.OrderBy(x => x.Process).ThenBy(x => x.Type)
            .Select(x => new ProcessTypeLinkDto(x.Id, x.Process, x.Type))
            .ToListAsync(ct));

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<ProcessTypeLinkDto>> Create(ProcessTypeLinkUpsert dto, CancellationToken ct)
    {
        var process = (dto.Process ?? string.Empty).Trim();
        var type = (dto.Type ?? string.Empty).Trim();
        if (process.Length == 0 || type.Length == 0)
            return BadRequest("Process and Type are both required.");

        if (await db.ProcessTypeLinks.AnyAsync(x => x.Process == process && x.Type == type, ct))
            return Conflict("That Process–Type link already exists.");

        var link = new ProcessTypeLink { Process = process, Type = type };
        db.ProcessTypeLinks.Add(link);
        await db.SaveChangesAsync(ct);
        return Ok(new ProcessTypeLinkDto(link.Id, link.Process, link.Type));
    }

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var link = await db.ProcessTypeLinks.FindAsync([id], ct);
        if (link is null) return NotFound();
        db.ProcessTypeLinks.Remove(link);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
