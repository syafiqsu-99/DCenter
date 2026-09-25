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
        => Ok(await db.Welders.AsNoTracking().OrderBy(w => w.WelderName)
            .Select(w => new WelderDto(w.Id, w.WelderName, w.WelderNo, w.IsActive, w.UsageScope))
            .ToListAsync(ct));

    [HttpGet("search")]
    public async Task<ActionResult<List<WelderDto>>> Search([FromQuery] string? q, [FromQuery] bool stockOnly = false, CancellationToken ct = default)
    {
        q = (q ?? string.Empty).Trim();
        var query = db.Welders.AsNoTracking().Where(w => w.IsActive);
        if (stockOnly) query = query.Where(w => w.UsageScope == WelderScope.ReportAndStock);
        if (q.Length > 0) query = query.Where(w => w.WelderName.Contains(q) || w.WelderNo.Contains(q));
        return Ok(await query.OrderBy(w => w.WelderName).Take(20)
            .Select(w => new WelderDto(w.Id, w.WelderName, w.WelderNo, w.IsActive, w.UsageScope))
            .ToListAsync(ct));
    }

    [SupervisorOnly]
    [HttpPost]
    public async Task<ActionResult<WelderDto>> Create(WelderDto dto, CancellationToken ct)
    {
        var (name, number, scope, error) = Validate(dto);
        if (error is not null) return BadRequest(error);

        var w = new Welder { WelderName = name!, WelderNo = number!, IsActive = dto.IsActive, UsageScope = scope! };
        db.Welders.Add(w);
        await db.SaveChangesAsync(ct);
        return Ok(new WelderDto(w.Id, w.WelderName, w.WelderNo, w.IsActive, w.UsageScope));
    }

    [SupervisorOnly]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, WelderDto dto, CancellationToken ct)
    {
        var (name, number, scope, error) = Validate(dto);
        if (error is not null) return BadRequest(error);

        var w = await db.Welders.FindAsync([id], ct);
        if (w is null) return NotFound();
        w.WelderName = name!;
        w.WelderNo = number!;
        w.IsActive = dto.IsActive;
        w.UsageScope = scope!;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [SupervisorOnly]
    [HttpPut("scope")]
    public async Task<ActionResult<int>> SetScope(WelderScopeUpdate dto, CancellationToken ct)
    {
        var scope = WelderScope.Normalize(dto.UsageScope);
        if (scope is null) return BadRequest("Scope must be Report or ReportAndStock.");
        var ids = (dto.Ids ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0) return BadRequest("Select at least one welder.");

        var updated = await db.Welders
            .Where(w => ids.Contains(w.Id))
            .ExecuteUpdateAsync(s => s.SetProperty(w => w.UsageScope, scope), ct);
        return Ok(updated);
    }

    [SupervisorOnly]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var w = await db.Welders.FindAsync([id], ct);
        if (w is null) return NotFound();
        if (await db.ConsumableMovements.AnyAsync(m => m.WelderId == id, ct) || await db.HoldingRecords.AnyAsync(h => h.WelderId == id, ct))
            return Conflict($"{w.WelderName} has consumable pickups or returns on record. Set the welder inactive instead.");
        db.Welders.Remove(w);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static (string? Name, string? Number, string? Scope, string? Error) Validate(WelderDto dto)
    {
        var name = dto.WelderName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 200) return (null, null, null, "Welder Name is required (max 200 characters).");
        var number = dto.WelderNo?.Trim() ?? string.Empty;
        if (number.Length > 50) return (null, null, null, "Welder No. is limited to 50 characters.");
        var scope = WelderScope.Normalize(dto.UsageScope ?? WelderScope.Report);
        if (scope is null) return (null, null, null, "Scope must be Report or ReportAndStock.");
        return (name, number, scope, null);
    }
}
