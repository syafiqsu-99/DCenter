using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrdersController(WorkOrderSearchService workOrders) : ControllerBase
{
    private const string MissingBomView =
        "The view vw_DCenter_BomTree is missing. Run Sql/OracleBetsyDB/DCenter_BomTree.sql on OracleBetsyDB first.";

    public record WorkOrderSearchResponse(List<WorkOrderSummary> Items, bool HasMore);
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
        catch (Exception ex) when (IsTimeout(ex))
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
        catch (Exception ex) when (IsTimeout(ex))
        {
            return StatusCode(504, "Loading the work order list took too long.");
        }
    }

    [HttpGet("{workOrderNumber}/header")]
    public async Task<ActionResult<WorkOrderHeader>> Header(string workOrderNumber, CancellationToken ct)
    {
        var wo = await workOrders.SummaryAsync(workOrderNumber, ct);
        return wo is null
            ? NoContent()
            : Ok(new WorkOrderHeader(workOrderNumber, wo.AssemblyItem, wo.AssemblyDesc));
    }

    [HttpGet("{workOrderNumber}/parts")]
    public async Task<ActionResult<List<WorkOrderNode>>> Parts(string workOrderNumber, CancellationToken ct)
    {
        try
        {
            return Ok(await workOrders.TreeForWorkOrderAsync(workOrderNumber, ct));
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return StatusCode(503, MissingBomView);
        }
        catch (Exception ex) when (IsTimeout(ex))
        {
            return StatusCode(504, "Loading the work order parts took too long.");
        }
    }

    private static bool IsTimeout(Exception ex) => ex is SqlException { Number: -2 } or TimeoutException;
}
