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

public class ReportCompletionTests
{
    private static Joint FullJoint(int number) => new()
    {
        JointNumber = number, WpsNo = "WPS-8", WelderName = "Ali", WelderNo = "W-01", HeatNumberLeft = "H1", HeatNumberRight = "H2",
        Materials = [new JointMaterial { ColumnNumber = 1, Process = "SMAW", Type = "E308L", HeatLot = "L9" }],
    };

    [Fact]
    public void CompleteReportHasNoProblems()
    {
        var report = new Report { Joints = [FullJoint(1), FullJoint(2)] };

        Assert.Empty(ReportSaveRules.CompletionProblems(report));
    }

    [Fact]
    public void ReportWithoutJointsCannotBeCompleted()
    {
        Assert.Equal(["Add at least one joint."], ReportSaveRules.CompletionProblems(new Report()));
    }

    [Fact]
    public void MissingFieldsAreNamedPerJoint()
    {
        var gaps = FullJoint(2);
        gaps.WpsNo = "  ";
        gaps.WelderNo = null;
        gaps.HeatNumberRight = "";
        gaps.Materials[0].HeatLot = null;
        var noElectrode = FullJoint(3);
        noElectrode.Materials = [];
        var report = new Report { Joints = [FullJoint(1), gaps, noElectrode] };

        var problems = ReportSaveRules.CompletionProblems(report);

        Assert.Equal(
        [
            "Joint 2: WPS No., Welder No., Heat Number (with), Electrode 1 Heat/Lot",
            "Joint 3: Electrode 1 Process, Electrode 1 Type, Electrode 1 Heat/Lot",
        ], problems);
    }

    [Fact]
    public void CompletionMessageListsTenJointsThenCounts()
    {
        var problems = Enumerable.Range(1, 12).Select(i => $"Joint {i}: WPS No.").ToList();

        var message = ReportSaveRules.CompletionMessage(problems);

        Assert.Contains("Joint 10: WPS No.", message);
        Assert.DoesNotContain("Joint 11:", message);
        Assert.EndsWith("and 2 more joint(s).", message);
    }

    [Fact]
    public void RenumberGivesOneToNInTheUsersOrder()
    {
        var joints = new List<JointDto>
        {
            new() { JointNumber = 5, WpsNo = "a" },
            new() { JointNumber = 2, WpsNo = "b" },
            new() { JointNumber = 2, WpsNo = "c" },
            new() { JointNumber = 9, WpsNo = "d" },
        };

        var renumbered = ReportSaveRules.Renumber(joints);

        Assert.Equal([1, 2, 3, 4], renumbered.Select(j => j.JointNumber));
        Assert.Equal(["b", "c", "a", "d"], renumbered.Select(j => j.WpsNo));
    }
}

public class ReportDocumentTests
{
    private static readonly DateOnly Today = new(2026, 9, 29);

    private static ResolvedSignOff Defaults(Report r, ReportSignOff? input = null)
        => ReportSignOffRules.Resolve(r, input, Today, "Aizat Karim");

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
        Assert.Equal(expected, WeldCardLayout.PartLabel(desc, no));
    }

    [Fact]
    public void Excel_ShowsPartNumbersAndIgnoresStoredSignOffFields()
    {
        var report = Sample();
        var bytes = new ExcelReportService().Generate(report, Defaults(report));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var values = wb.Worksheet(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains("Joining of Body (P-1) with Flange (P-2)", values);
        Assert.DoesNotContain("Should not print", values);
        Assert.Contains("Ali (ID W-01)", values);
        Assert.Contains("SMAW", values);
        Assert.Contains("WO100", values);
    }

    [Fact]
    public void Excel_DefaultsEngineerToTodayAndLeavesQaBlank()
    {
        var report = Sample();
        var bytes = new ExcelReportService().Generate(report, Defaults(report));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var values = wb.Worksheet(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains("Aizat Karim", values);
        Assert.Equal(1, values.Count(v => v == "29/9/2026"));
        Assert.Equal(2, values.Count(v => v == "20/9/2026"));
    }

    [Fact]
    public void Excel_PrintsTheSignOffEnteredInTheDialog()
    {
        var report = Sample();
        var input = new ReportSignOff("Siti Engineer", new DateOnly(2026, 10, 1), "Lim QA", new DateOnly(2026, 10, 2),
            [new WelderSignOff("ali", "w-01", "Ali bin Abu (ID W-01)", new DateOnly(2026, 9, 21))]);
        var bytes = new ExcelReportService().Generate(report, Defaults(report, input));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var values = wb.Worksheet(1).CellsUsed().Select(c => c.GetString()).ToList();

        Assert.Contains("Siti Engineer", values);
        Assert.Contains("1/10/2026", values);
        Assert.Contains("Lim QA", values);
        Assert.Contains("2/10/2026", values);
        Assert.Contains("Ali bin Abu (ID W-01)", values);
        Assert.Contains("21/9/2026", values);
        Assert.DoesNotContain("Aizat Karim", values);
    }

    [Fact]
    public void Layout_PrintsDashesAndTheJointNumber()
    {
        var report = Sample(2);
        var header = WeldCardLayout.Header(report);
        var joint = WeldCardLayout.Joint(report.Joints[1], "20/9/2026", Defaults(report));

        Assert.Equal("-", header.Cells.Single(c => c.Row == 4 && c.Col == 1).Text);
        Assert.Equal("-", header.Cells.Single(c => c.Row == 2 && c.Col == 17).Text);
        Assert.Equal("2", joint.Cells.Single(c => c.Row == 3 && c.Col == 1).Text);
        Assert.All(joint.Cells.Where(c => c.Col == 10 && (c.Row == 4 || c.Row == 6)), c => Assert.Equal("-", c.Text));
    }

    [Fact]
    public void SignOffDefaults_ListEachWelderOnceWithTheDateWelded()
    {
        var report = Sample(3);
        report.Joints[2].WelderName = "Ravi";
        report.Joints[2].WelderNo = "W-07";

        var defaults = ReportSignOffRules.Defaults(report, Today, "Aizat Karim");

        Assert.Equal("Aizat Karim", defaults.EngineerName);
        Assert.Equal(Today, defaults.EngineerDate);
        Assert.Equal("", defaults.QaName);
        Assert.Null(defaults.QaDate);
        Assert.Equal(["Ali (ID W-01)", "Ravi (ID W-07)"], defaults.Welders!.Select(w => w.DisplayName));
        Assert.All(defaults.Welders!, w => Assert.Equal(report.DateWelded, w.Date));
    }

    [Fact]
    public void SignOffResolve_KeepsDefaultsForWeldersNotInTheInputAndIgnoresUnknownOnes()
    {
        var report = Sample(2);
        report.Joints[1].WelderName = "Ravi";
        report.Joints[1].WelderNo = "W-07";
        var input = new ReportSignOff("  Eng  ", null, null, null,
            [new WelderSignOff("Nobody", "X", "Ghost", null), new WelderSignOff("Ravi", "W-07", new string('r', 150), null)]);

        var resolved = Defaults(report, input);

        Assert.Equal("Eng", resolved.EngineerName);
        Assert.Equal("", resolved.EngineerDate);
        Assert.Equal(("Ali (ID W-01)", "20/9/2026"), resolved.WelderFor(report.Joints[0]));
        Assert.Equal(ReportSignOffRules.MaxNameLength, resolved.WelderFor(report.Joints[1])!.Value.Name.Length);
        Assert.Equal(2, resolved.Welders.Count);
    }

    [Fact]
    public void Layout_FillsEveryGridSlotExactlyOnce()
    {
        var report = Sample();
        foreach (var section in new[] { WeldCardLayout.Header(report), WeldCardLayout.Joint(report.Joints[0], "20/9/2026", Defaults(report)) })
        {
            var hits = new int[section.RowHeights.Count + 1, WeldCardLayout.Columns + 1];
            foreach (var c in section.Cells)
                for (var r = c.Row; r < c.Row + c.RowSpan; r++)
                    for (var k = c.Col; k < c.Col + c.ColSpan; k++)
                        hits[r, k]++;

            for (var r = 1; r <= section.RowHeights.Count; r++)
                for (var k = 1; k <= WeldCardLayout.Columns; k++)
                    Assert.Equal(1, hits[r, k]);
        }
    }

    [Fact]
    public void Layout_KeepsJointsWholeAndStartsNewPagesOnlyWhenFull()
    {
        var report = Sample();
        var first = WeldCardLayout.BannerHeight + WeldCardLayout.Header(report).Height;
        var joint = WeldCardLayout.Joint(report.Joints[0], "", Defaults(report)).Height;

        var perFirstPage = (int)((WeldCardLayout.PrintableHeight - first) / joint);
        var perPage = (int)(WeldCardLayout.PrintableHeight / joint);

        Assert.Empty(WeldCardLayout.PageStarts(first, Enumerable.Repeat(joint, perFirstPage)));
        Assert.Equal(
            [perFirstPage, perFirstPage + perPage],
            WeldCardLayout.PageStarts(first, Enumerable.Repeat(joint, perFirstPage + perPage + 1)));
    }

    [Fact]
    public void Excel_PrintsOnA4PortraitWithPageBreaksBetweenJoints()
    {
        var report = Sample(20);
        var bytes = new ExcelReportService().Generate(report, Defaults(report));

        using var wb = new XLWorkbook(new MemoryStream(bytes));
        var setup = wb.Worksheet(1).PageSetup;

        Assert.Equal(XLPaperSize.A4Paper, setup.PaperSize);
        Assert.Equal(XLPageOrientation.Portrait, setup.PageOrientation);
        Assert.NotEmpty(setup.RowBreaks);
    }

    [Fact]
    public void Pdf_GeneratesForEmptyAndFullReports()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var pdf = new PdfReportService();

        var empty = new Report { WorkOrderNumber = "WO0" };
        var full = Sample(50);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(pdf.Generate(empty, Defaults(empty)), 0, 4));
        Assert.True(pdf.Generate(full, Defaults(full)).Length > 1000);
    }

    [Fact]
    public void SignOffJsonFromTheDialogBinds()
    {
        const string json = """
            {"engineerName":"Aizat Karim","engineerDate":"2026-09-29","qaName":"Lim QA","qaDate":null,
             "welders":[{"welderName":"Ali","welderNo":"W-01","displayName":"Ali bin Abu (ID W-01)","date":"2026-09-20"}]}
            """;

        var signOff = System.Text.Json.JsonSerializer.Deserialize<ReportSignOff>(
            json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        Assert.Equal(new DateOnly(2026, 9, 29), signOff.EngineerDate);
        Assert.Null(signOff.QaDate);
        Assert.Equal("Ali bin Abu (ID W-01)", Assert.Single(signOff.Welders!).DisplayName);
    }
}
