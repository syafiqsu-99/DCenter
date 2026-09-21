using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrdersController(WorkOrderSearchService workOrders) : ControllerBase
{
    public record WorkOrderSearchResponse(List<WorkOrderRow> Items, bool HasMore);
    public record WorkOrderHeader(string WorkOrderNumber, string? PartNo, string? Description);

    [HttpGet("search")]
    public async Task<ActionResult<WorkOrderSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int skip = 0,
        [FromQuery] int take = WorkOrderSearchService.DefaultPageSize,
        CancellationToken ct = default)
    {
        try
        {
            var (items, hasMore) = await workOrders.SearchAsync(q, skip, take, ct);
            return Ok(new WorkOrderSearchResponse(items, hasMore));
        }
        catch (Exception ex) when (ex is SqlException { Number: -2 } or TimeoutException)
        {
            return StatusCode(504, "The work order search took too long. Try a more specific work order number.");
        }
    }

    [HttpGet("numbers")]
    public async Task<ActionResult<List<string>>> Numbers(CancellationToken ct)
    {
        try
        {
            return Ok(await workOrders.AllWorkOrderNumbersAsync(ct));
        }
        catch (Exception ex) when (ex is SqlException { Number: -2 } or TimeoutException)
        {
            return StatusCode(504, "Loading the work order list took too long.");
        }
    }

    [HttpGet("{workOrderNumber}/header")]
    public async Task<ActionResult<WorkOrderHeader>> Header(string workOrderNumber, CancellationToken ct)
    {
        var parts = await workOrders.PartsForWorkOrderAsync(workOrderNumber, ct);
        var first = parts.FirstOrDefault();
        return first is null
            ? NoContent()
            : Ok(new WorkOrderHeader(workOrderNumber, first.AssemblyItem, first.ItemDesc));
    }

    [HttpGet("{workOrderNumber}/parts")]
    public async Task<ActionResult<List<WorkOrderRow>>> Parts(string workOrderNumber, CancellationToken ct)
    => Ok(await workOrders.PartsForWorkOrderAsync(workOrderNumber, ct));
}