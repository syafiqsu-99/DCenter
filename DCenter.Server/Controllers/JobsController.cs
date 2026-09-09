using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController(JobSearchService jobs) : ControllerBase
{
    // GET /api/jobs/top -> top 1000 rows for the browse table (unfiltered).
    [HttpGet("top")]
    public async Task<ActionResult<List<JobRow>>> Top(CancellationToken ct)
        => Ok(await jobs.TopAsync(ct));

    // GET /api/jobs/search?jobNumber=47762415
    // Returns the matching rows. distinctJobCount lets the client decide whether to
    // show the confirm button (exactly one distinct job number).
    [HttpGet("search")]
    public async Task<ActionResult<JobSearchResult>> Search(
        [FromQuery] string jobNumber, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jobNumber))
            return BadRequest("jobNumber is required.");

        var rows = await jobs.SearchAsync(jobNumber.Trim(), ct);
        var distinct = rows.Select(r => r.JobNumber).Distinct().ToList();
        var resolved = distinct.Count == 1 ? distinct[0] : jobNumber.Trim();

        return Ok(new JobSearchResult(resolved, rows.Count, rows));
    }
}