using DCenter.Server.Data;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeldersController(WeldReportContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<WelderDto>>> GetAll(CancellationToken ct)
        => Ok(await db.Welders.OrderBy(w => w.WelderName)
            .Select(w => new WelderDto(w.Id, w.WelderName, w.WelderNo, w.IsActive))
            .ToListAsync(ct));

    [HttpGet("search")]
    public async Task<ActionResult<List<WelderDto>>> Search([FromQuery] string? q, CancellationToken ct)
    {
        q = (q ?? string.Empty).Trim();
        var query = db.Welders.Where(w => w.IsActive);
        if (q.Length > 0)
            query = query.Where(w => w.WelderName.Contains(q) || w.WelderNo.Contains(q));
        return Ok(await query.OrderBy(w => w.WelderName).Take(20)
            .Select(w => new WelderDto(w.Id, w.WelderName, w.WelderNo, w.IsActive))
            .ToListAsync(ct));
    }

    [HttpPost]
    public async Task<ActionResult<WelderDto>> Create(WelderDto dto, CancellationToken ct)
    {
        var w = new Welder { WelderName = dto.WelderName, WelderNo = dto.WelderNo, IsActive = dto.IsActive };
        db.Welders.Add(w);
        await db.SaveChangesAsync(ct);
        return Ok(new WelderDto(w.Id, w.WelderName, w.WelderNo, w.IsActive));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WelderDto dto, CancellationToken ct)
    {
        var w = await db.Welders.FindAsync([id], ct);
        if (w is null) return NotFound();
        w.WelderName = dto.WelderName;
        w.WelderNo = dto.WelderNo;
        w.IsActive = dto.IsActive;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var w = await db.Welders.FindAsync([id], ct);
        if (w is null) return NotFound();
        db.Welders.Remove(w);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}