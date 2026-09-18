using DCenter.Server.Data;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/weldreference")]
public class WeldReferenceController(WeldReportContext db, WpsFilterService filter) : ControllerBase
{
    [HttpGet("wps-for-workorder/{workOrderNumber}")]
    public async Task<ActionResult<WpsFilterResult>> WpsForWorkOrder(string workOrderNumber, CancellationToken ct)
        => Ok(await filter.ForWorkOrderAsync(workOrderNumber, ct));

    [HttpGet("wps-for-mrn")]
    public async Task<ActionResult<WpsFilterResult>> WpsForMrn([FromQuery] string mrn, CancellationToken ct)
        => string.IsNullOrWhiteSpace(mrn)
            ? BadRequest("An MRN is required.")
            : Ok(await filter.ForMrnsAsync([mrn.Trim()], ct));

    [HttpGet("counts")]
    public async Task<ActionResult<object>> Counts(CancellationToken ct) => Ok(new
    {
        mrnSpecs = await db.MrnSpecs.CountAsync(ct),
        bpvc = await db.BpvcMaterials.CountAsync(ct),
        wpsItems = await db.WpsItems.CountAsync(ct),
    });
}