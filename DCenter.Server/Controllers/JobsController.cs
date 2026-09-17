using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController(JobSearchService jobs) : ControllerBase
{
    public record JobSearchResponse(List<JobRow> Items, bool HasMore);

    [HttpGet("search")]
    public async Task<ActionResult<JobSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int skip = 0,
        [FromQuery] int take = JobSearchService.DefaultPageSize,
        CancellationToken ct = default)
    {
        try
        {
            var (items, hasMore) = await jobs.SearchAsync(q, skip, take, ct);
            return Ok(new JobSearchResponse(items, hasMore));
        }
        catch (Exception ex) when (ex is SqlException { Number: -2 } or TimeoutException)
        {
            return StatusCode(504, "The job search took too long. Try a more specific job number.");
        }
    }
}