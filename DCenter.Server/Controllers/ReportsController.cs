using DCenter.Server.Models;
using DCenter.Server.Services;
using Microsoft.AspNetCore.Mvc;

namespace DCenter.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController(
    ReportService reports,
    PdfReportService pdf,
    ExcelReportService excel) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ReportSummary>>> List(CancellationToken ct)
        => Ok(await reports.ListAsync(ct));

    [HttpGet("{workOrderNumber}")]
    public async Task<ActionResult<ReportDto>> Get(string workOrderNumber, CancellationToken ct)
    {
        var dto = await reports.LoadAsync(workOrderNumber, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost("{workOrderNumber}/complete")]
    public async Task<IActionResult> Complete(string workOrderNumber, [FromBody] bool complete, CancellationToken ct)
        => await reports.MarkCompleteAsync(workOrderNumber, complete, ct) switch
        {
            ReportService.CompleteResult.Ok => NoContent(),
            ReportService.CompleteResult.DateWeldedRequired => BadRequest("Date welded is required to mark a report complete."),
            _ => NotFound(),
        };

    [HttpDelete("{workOrderNumber}")]
    public async Task<IActionResult> Delete(string workOrderNumber, CancellationToken ct)
        => await reports.DeleteAsync(workOrderNumber, ct) switch
        {
            ReportService.DeleteResult.Ok => NoContent(),
            ReportService.DeleteResult.Completed => Conflict("Completed reports cannot be deleted. Reopen the report first."),
            _ => NotFound(),
        };

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
        if (dto.DateWelded is null)
            return BadRequest("Date welded is required to save a report.");
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