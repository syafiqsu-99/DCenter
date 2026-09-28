using ClosedXML.Excel;
using DCenter.Server.Entities;
using DCenter.Server.Models;
using DCenter.Server.Services;

namespace DCenter.Server.Tests;

public class ReportSaveRulesTests
{
    private const string Version = "AAAAAAAAB9E=";

    private static Report Existing(DateTime? completedAt = null) =>
        new() { Id = 5, WorkOrderNumber = "WO100", CompletedAt = completedAt, RowVersion = Convert.FromBase64String(Version) };

    [Fact]
    public void NewReportMayBeInserted()
    {
        Assert.Null(ReportSaveRules.Conflict(null, new ReportDto { WorkOrderNumber = "WO100" }));
    }

    [Fact]
    public void EditOfTheLoadedReportMayProceed()
    {
        Assert.Null(ReportSaveRules.Conflict(Existing(), new ReportDto { Id = 5, WorkOrderNumber = "WO100", RowVersion = Version }));
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(0, Version)]
    [InlineData(5, null)]
    [InlineData(5, "")]
    public void SavingOverAnExistingReportWithoutItsVersionIsRefused(int id, string? rowVersion)
    {
        var refusal = ReportSaveRules.Conflict(Existing(), new ReportDto { Id = id, WorkOrderNumber = "WO100", RowVersion = rowVersion });

        Assert.NotNull(refusal);
        Assert.Contains("already exists", refusal);
    }

    [Fact]
    public void CompletedReportsCannotBeSaved()
    {
        var refusal = ReportSaveRules.Conflict(Existing(DateTime.Now),
            new ReportDto { Id = 5, WorkOrderNumber = "WO100", RowVersion = Version });

        Assert.Contains("completed", refusal);
    }

    [Fact]
    public void MalformedVersionStampIsRefused()
    {
        var refusal = ReportSaveRules.Conflict(Existing(), new ReportDto { Id = 5, WorkOrderNumber = "WO100", RowVersion = "not base64!" });

        Assert.Contains("version stamp", refusal);
    }
}

public class ReportDocumentTests
{
    private static Report Sample(int joints = 1)
    {
        var report = new Report
        {
            WorkOrderNumber = "WO100", PartNo = "ASM-1", Description = "Valve body", DateWelded = new DateOnly(2026, 9, 20),
            MaterialSpec1 = "SA-240", Grade1 = "304", PNumber1 = "8",
            EngineerSupervisor = "Should not print", QaInspector = "Should not print",
        };
        for (var i = 1; i <= joints; i++)
        {
            report.Joints.Add(new Joint
            {
                JointNumber = i, PartDescLeft = "Body", PartNoLeft = "P-1", PartDescRight = "Flange", PartNoRight = "P-2",
                WpsNo = "WPS-8", WelderName = "Ali", WelderNo = "W-01",
                Materials =
                [
                    new JointMaterial { ColumnNumber = 1, Process = "SMAW", Size = "3.2", Type = "E308L", Manuf = "Kobelco", HeatLot = "L9" },
                ],
            });
        }
        return report;
    }

    [Theory]
    [InlineData("Body", "P-1", "Body (P-1)")]
    [InlineData("Body", "", "Body")]
    [InlineData("", "P-1", "P-1")]
    [InlineData(null, null, "-")]
    public void PartLabel_CombinesDescriptionAndNumber(string? desc, string? no, string expected)
    {
        Assert.Equal(expected, ExcelReportService.PartLabel(desc, no));
    }

    [Fact]
    public void Excel_ShowsPartNumbersAndLeavesSignOffsBlank()
    {
        var bytes = new ExcelReportService().Generate(Sample());

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var values = wb.Worksheet(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains("Joining of Body (P-1) with Flange (P-2)", values);
        Assert.DoesNotContain("Should not print", values);
        Assert.Contains("Ali (ID W-01)", values);
        Assert.Contains("SMAW", values);
        Assert.Contains("WO100", values);
    }

    [Fact]
    public void Excel_EngineerAndQaDatesAreBlankButWelderDateIsFilled()
    {
        var bytes = new ExcelReportService().Generate(Sample());

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var dateCells = wb.Worksheet(1).CellsUsed().Count(c => c.GetString() == "20/9/2026");

        Assert.Equal(2, dateCells);
    }

    [Fact]
    public void Pdf_GeneratesForEmptyAndFullReports()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var pdf = new PdfReportService();

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Generate(new Report { WorkOrderNumber = "WO0" }), 0, 4));
        Assert.True(pdf.Generate(Sample(50)).Length > 1000);
    }
}
