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
    // Load existing draft for a job number (null-ish -> 204 so client can start fresh).
    [HttpGet("{jobNumber}")]
    public async Task<ActionResult<ReportDto>> Get(string jobNumber, CancellationToken ct)
    {
        var dto = await reports.LoadAsync(jobNumber, ct);
        return dto is null ? NoContent() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<ReportDto>> Save(ReportDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.JobNumber))
            return BadRequest("JobNumber is required.");
        return Ok(await reports.SaveAsync(dto, ct));
    }

    // PDF for on-screen display (inline).
    [HttpGet("{jobNumber}/pdf")]
    public async Task<IActionResult> Pdf(string jobNumber, CancellationToken ct)
    {
        var r = await reports.GetEntityAsync(jobNumber, ct);
        if (r is null) return NotFound();
        var bytes = pdf.Generate(r);
        return File(bytes, "application/pdf");
    }

    // Excel download (attachment).
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
