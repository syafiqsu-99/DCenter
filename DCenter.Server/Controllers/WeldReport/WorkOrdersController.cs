using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WorkOrdersController(WorkOrderSearchService workOrders) : ControllerBase
{
    private const string MissingViews =
        "The DCenter work order views are missing. Run Sql/DCenter/DCenter_SourceViews.sql on the DCenter database.";
    private const string NoSourceAccess =
        "The DCenter database login cannot read OracleBetsyDB. Grant it SELECT on the tables listed in Sql/DCenter/DCenter_SourceViews.sql.";

    public record WorkOrderSearchResponse(List<WorkOrderSummary> Items, bool HasMore);
    public record WorkOrderHeader(string WorkOrderNumber, string? PartNo, string? Description);

    [HttpGet("search")]
    public Task<ActionResult<WorkOrderSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery] int skip = 0,
        [FromQuery] int take = WorkOrderSearchService.DefaultPageSize,
        CancellationToken ct = default)
        => Guard("The work order search took too long. Try a more specific work order number.", async () =>
        {
            var (items, hasMore) = await workOrders.SearchAsync(q, skip, take, ct);
            return (ActionResult<WorkOrderSearchResponse>)Ok(new WorkOrderSearchResponse(items, hasMore));
        });

    [HttpGet("numbers")]
    public Task<ActionResult<List<string>>> Numbers(CancellationToken ct)
        => Guard("Loading the work order list took too long.", async () =>
            (ActionResult<List<string>>)Ok(await workOrders.AllWorkOrderNumbersAsync(ct)));

    [HttpGet("{workOrderNumber}/header")]
    public Task<ActionResult<WorkOrderHeader>> Header(string workOrderNumber, CancellationToken ct)
        => Guard("Loading the work order took too long.", async () =>
        {
            var wo = await workOrders.SummaryAsync(workOrderNumber, ct);
            return wo is null
                ? (ActionResult<WorkOrderHeader>)NoContent()
                : Ok(new WorkOrderHeader(workOrderNumber, wo.AssemblyItem, wo.AssemblyDesc));
        });

    [HttpGet("{workOrderNumber}/parts")]
    public Task<ActionResult<List<WorkOrderNode>>> Parts(string workOrderNumber, CancellationToken ct)
        => Guard("Loading the work order parts took too long.", async () =>
            (ActionResult<List<WorkOrderNode>>)Ok(await workOrders.TreeForWorkOrderAsync(workOrderNumber, ct)));

    private async Task<ActionResult<T>> Guard<T>(string timeoutMessage, Func<Task<ActionResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (Exception ex) when (IsTimeout(ex))
        {
            return StatusCode(504, timeoutMessage);
        }
        catch (SqlException ex) when (ex.Number == 208)
        {
            return StatusCode(503, MissingViews);
        }
        catch (SqlException ex) when (ex.Number is 229 or 916 or 4060)
        {
            return StatusCode(503, NoSourceAccess);
        }
    }

    private static bool IsTimeout(Exception ex) => ex is SqlException { Number: -2 } or TimeoutException;
}
