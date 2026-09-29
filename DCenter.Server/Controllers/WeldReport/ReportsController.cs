using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController(
    ReportService reports,
    ReportInsightsService insights,
    PdfReportService pdf,
    ExcelReportService excel,
    SupervisorAuth supervisors,
    ILogger<ReportsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReportSummary>>> List(CancellationToken ct)
        => Ok(await reports.ListAsync(ct));

    [HttpGet("dashboard")]
    public async Task<ActionResult<ReportDashboardDto>> Dashboard([FromQuery] int months = 6, CancellationToken ct = default)
        => Ok(await insights.GetDashboardAsync(months, ct));

    [HttpGet("trace")]
    public async Task<ActionResult<TraceResponse>> Trace([FromQuery] string? field, [FromQuery] string? q, CancellationToken ct)
    {
        var term = q?.Trim() ?? "";
        if (field is null || !ReportInsightsService.TraceFields.Contains(field)) return BadRequest("Unknown search field.");
        if (term.Length < 2) return BadRequest("Type at least 2 characters to search.");
        return Ok(await insights.TraceAsync(field, term, ct));
    }

    [HttpGet("{workOrderNumber}")]
    public async Task<ActionResult<ReportDto>> Get(string workOrderNumber, CancellationToken ct)
    {
        var dto = await reports.LoadAsync(workOrderNumber, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost("{workOrderNumber}/complete")]
    public async Task<IActionResult> Complete(string workOrderNumber, [FromBody] bool complete, CancellationToken ct)
    {
        var supervisor = supervisors.FromRequest(Request);
        if (!complete && supervisor is null)
            return StatusCode(StatusCodes.Status401Unauthorized, "Only a supervisor can reopen a completed report. Log in as supervisor and try again.");
        var outcome = await reports.MarkCompleteAsync(workOrderNumber, complete, supervisor?.Name, ct);
        return outcome.Result switch
        {
            ReportService.CompleteResult.Ok => NoContent(),
            ReportService.CompleteResult.DateWeldedRequired => BadRequest("Date welded is required to mark a report complete."),
            ReportService.CompleteResult.Incomplete => BadRequest(ReportSaveRules.CompletionMessage(outcome.Problems)),
            _ => NotFound(),
        };
    }

    [HttpDelete("{workOrderNumber}")]
    [SupervisorOnly]
    public async Task<IActionResult> Delete(string workOrderNumber, CancellationToken ct)
    {
        var result = await reports.DeleteAsync(workOrderNumber, ct);
        if (result == ReportService.DeleteResult.Ok)
            logger.LogInformation("Report draft {WorkOrder} deleted by {Supervisor}", workOrderNumber, supervisors.FromRequest(Request)?.Name);
        return result switch
        {
            ReportService.DeleteResult.Ok => NoContent(),
            ReportService.DeleteResult.Completed => Conflict("Completed reports cannot be deleted. Reopen the report first."),
            _ => NotFound(),
        };
    }

    [HttpGet("{workOrderNumber}/history")]
    public async Task<ActionResult<List<ReportStatusEventDto>>> History(string workOrderNumber, CancellationToken ct)
    {
        var history = await reports.GetHistoryAsync(workOrderNumber, ct);
        return history is null ? NotFound() : Ok(history);
    }

    [HttpPost]
    public async Task<ActionResult<ReportDto>> Save(ReportDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.WorkOrderNumber))
            return BadRequest("Work order number is required.");
        if (dto.WorkOrderNumber.Length > ReportSaveRules.MaxWorkOrderLength)
            return BadRequest($"Work order number is limited to {ReportSaveRules.MaxWorkOrderLength} characters.");
        if (dto.DateWelded is null)
            return BadRequest("Date welded is required to save a report.");
        if (dto.Joints.Count > ReportSaveRules.MaxJoints)
            return BadRequest($"A report can have at most {ReportSaveRules.MaxJoints} joints.");
        try
        {
            return Ok(await reports.SaveAsync(dto, ct));
        }
        catch (ReportConflictException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpGet("{workOrderNumber}/pdf")]
    public async Task<IActionResult> Pdf(string workOrderNumber, CancellationToken ct)
    {
        var r = await reports.GetEntityAsync(workOrderNumber, ct);
        if (r is null) return NotFound();
        var bytes = pdf.Generate(r);
        return File(bytes, "application/pdf");
    }

    [HttpGet("{workOrderNumber}/excel")]
    public async Task<IActionResult> Excel(string workOrderNumber, CancellationToken ct)
    {
        var r = await reports.GetEntityAsync(workOrderNumber, ct);
        if (r is null) return NotFound();
        var bytes = excel.Generate(r);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"WeldOrderCard_{workOrderNumber}.xlsx");
    }
}