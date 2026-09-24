using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/consumables")]
[SupervisorOnly]
public class ConsumableCountsController(StockCountService counts) : ConsumableControllerBase
{
    [HttpGet("count-sheet")]
    public async Task<ActionResult<CountSheetDto>> Sheet([FromQuery] string? scope, [FromQuery] string? category, CancellationToken ct)
        => ToAction(await counts.GetSheetAsync(scope, category, ct));

    [HttpPost("stock-counts")]
    public Task<ActionResult<StockCountDto>> Post(StockCountRequest request, CancellationToken ct)
        => Locked(() => counts.PostAsync(request, EnteredBy, ct));

    [HttpGet("stock-counts")]
    public async Task<ActionResult<StockCountPage>> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? scope,
        [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default)
        => Ok(await counts.GetCountsAsync(from, to, scope, skip, take, ct));

    [HttpGet("stock-counts/{referenceNo}")]
    public async Task<ActionResult<StockCountDetailDto>> Detail(string referenceNo, CancellationToken ct)
        => ToAction(await counts.GetCountAsync(referenceNo, ct));
}
