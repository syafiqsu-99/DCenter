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

    [HttpGet("{jobNumber}")]
    public async Task<ActionResult<ReportDto>> Get(string jobNumber, CancellationToken ct)
    {
        var dto = await reports.LoadAsync(jobNumber, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost("{jobNumber}/complete")]
    public async Task<IActionResult> Complete(string jobNumber, [FromBody] bool complete, CancellationToken ct)
        => await reports.MarkCompleteAsync(jobNumber, complete, ct) switch
        {
            ReportService.CompleteResult.Ok => NoContent(),
            ReportService.CompleteResult.DateWeldedRequired => BadRequest("Date welded is required to mark a report complete."),
            _ => NotFound(),
        };

    [HttpDelete("{jobNumber}")]
    public async Task<IActionResult> Delete(string jobNumber, CancellationToken ct)
        => await reports.DeleteAsync(jobNumber, ct) switch
        {
            ReportService.DeleteResult.Ok => NoContent(),
            ReportService.DeleteResult.Completed => Conflict("Completed reports cannot be deleted. Reopen the report first."),
            _ => NotFound(),
        };

    [HttpGet("{jobNumber}/history")]
    public async Task<ActionResult<List<ReportStatusEventDto>>> History(string jobNumber, CancellationToken ct)
    {
        var history = await reports.GetHistoryAsync(jobNumber, ct);
        return history is null ? NotFound() : Ok(history);
    }

    [HttpPost]
    public async Task<ActionResult<ReportDto>> Save(ReportDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.JobNumber))
            return BadRequest("JobNumber is required.");
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

    [HttpGet("{jobNumber}/pdf")]
    public async Task<IActionResult> Pdf(string jobNumber, CancellationToken ct)
    {
        var r = await reports.GetEntityAsync(jobNumber, ct);
        if (r is null) return NotFound();
        var bytes = pdf.Generate(r);
        return File(bytes, "application/pdf");
    }

    [HttpGet("{jobNumber}/excel")]
    public async Task<IActionResult> Excel(string jobNumber, CancellationToken ct)
    {
        var r = await reports.GetEntityAsync(jobNumber, ct);
        if (r is null) return NotFound();
        var bytes = excel.Generate(r);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"WeldOrderCard_{jobNumber}.xlsx");
    }
}