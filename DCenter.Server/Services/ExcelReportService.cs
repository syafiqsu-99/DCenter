using ClosedXML.Excel;
using DCenter.Server.Entities;

namespace DCenter.Server.Services;

public class ExcelReportService
{
    private static readonly string EmersonLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "emerson.png");
    private static readonly string FisherLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "fisher.png");

    // 10 columns (A..J) mirror the form grid.
    public byte[] Generate(Report r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Weld Order Card");
        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 8;
        for (int c = 1; c <= 10; c++) ws.Column(c).Width = 12;

        var dateStr = r.DateWelded?.ToString("d/M/yyyy") ?? "";
        int row = 1;

        // ---- Logos + company header ----
        if (File.Exists(EmersonLogo))
            ws.AddPicture(EmersonLogo).MoveTo(ws.Cell(row, 1)).WithSize(150, 42);
        if (File.Exists(FisherLogo))
            ws.AddPicture(FisherLogo).MoveTo(ws.Cell(row, 10)).WithSize(80, 34);

        ws.Cell(row + 1, 6).Value = "Emerson Process Management Manufacturing (M) Sdn Bhd";
        ws.Cell(row + 2, 6).Value = "Lot 13111, Mukim Labu Kawasan Perindustrian Labu";
        ws.Cell(row + 3, 6).Value = "71807 Nilai, Negeri Sembilan";
        ws.Cell(row + 4, 6).Value = "Tel: +60-6-795 2828";
        for (int i = 1; i <= 4; i++) ws.Cell(row + i, 6).Style.Font.FontSize = 8;
        row += 6;

        var title = ws.Range(row, 1, row, 10).Merge();
        title.Value = "Weld Order Card";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 12;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row += 2;

        int gridTop = row;

        // ---- Header block: Part No / Desc / Job No / Piece S/N / Date ----
        LabelValueStacked(ws, row, 1, 2, "Part No.", r.PartNo);
        LabelValueStacked(ws, row, 3, 2, "Part Description", r.Description);
        LabelValueStacked(ws, row, 5, 2, "Job No.", r.JobNumber);
        LabelValueStacked(ws, row, 7, 2, "Piece S/N", "-");
        LabelValueStacked(ws, row, 9, 2, "Date", dateStr);
        row += 2;

        // ---- MRP/CSP + Material Welded (spec/grade/P# rows 1..3) + instruction ----
        ws.Cell(row, 1).Value = "MRP/CSP No.";
        ws.Cell(row, 1).Style.Font.Bold = true;
        // Material Welded label spans the three spec rows (col 2)
        var matLabel = ws.Range(row + 1, 1, row + 3, 1).Merge();
        matLabel.Value = "Material\nWelded";
        matLabel.Style.Font.Bold = true;
        matLabel.Style.Alignment.WrapText = true;
        matLabel.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        // three spec/grade/P# rows
        string[] specs = { r.MaterialSpec1 ?? "", r.MaterialSpec2 ?? "", r.MaterialSpec3 ?? "" };
        string[] grades = { r.Grade1 ?? "", r.Grade2 ?? "", r.Grade3 ?? "" };
        string[] pnums = { r.PNumber1 ?? "", r.PNumber2 ?? "", r.PNumber3 ?? "" };
        for (int i = 0; i < 3; i++)
        {
            int rr = row + 1 + i;
            ws.Cell(rr, 2).Value = "Spec"; ws.Cell(rr, 2).Style.Font.Bold = true;
            ws.Cell(rr, 3).Value = specs[i];
            ws.Cell(rr, 4).Value = "Grade"; ws.Cell(rr, 4).Style.Font.Bold = true;
            ws.Cell(rr, 5).Value = grades[i];
            ws.Cell(rr, 6).Value = "P#"; ws.Cell(rr, 6).Style.Font.Bold = true;
            ws.Cell(rr, 7).Value = pnums[i];
        }

        var instr = ws.Range(row + 1, 8, row + 3, 10).Merge();
        instr.Value = "RECORD HEAT NO. PIECE SERIAL NO. AND WELD MATERIAL FOR "
                    + "EACH WELD JOINT/REPAIR. RECORD WELD JOINT NUMBER(S) IF "
                    + "HEAT NO. OR PIECE SERIAL NO. IS NOT REQUIRED";
        instr.Style.Font.Bold = true;
        instr.Style.Alignment.WrapText = true;
        instr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        row += 4;

        // ---- Joint blocks ----
        foreach (var j in r.Joints.OrderBy(x => x.JointNumber))
            row = JointBlock(ws, row, j, r, dateStr);

        // ---- Footer ----
        ws.Cell(row, 1).Value = "F-WD-005 (Rev : 00)";
        ws.Cell(row, 1).Style.Font.Bold = true;

        // Borders across the whole grid.
        var used = ws.Range(gridTop, 1, row, 10);
        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // One joint = 6 rows matching the form: header row, then 4 weld-material rows, then joint desc.
    private static int JointBlock(IXLWorksheet ws, int row, Joint j, Report r, string dateStr)
    {
        var m = j.Materials.OrderBy(x => x.ColumnNumber).ToList();
        string V(int col, Func<JointMaterial, string?> pick)
            => m.FirstOrDefault(x => x.ColumnNumber == col) is { } hit
                ? (string.IsNullOrWhiteSpace(pick(hit)) ? "-" : pick(hit)!) : "-";

        string welder = string.IsNullOrWhiteSpace(j.WelderName)
            ? "" : $"{j.WelderName} (ID {j.WelderNo})";

        // Row 1: block header
        ws.Cell(row, 1).Value = "Fab No./Repair\nNCR-DVR No";
        ws.Cell(row, 2).Value = "FMP/FWPS No.";
        ws.Cell(row, 3).Value = "Rev";
        ws.Cell(row, 4).Value = "Amend.\nNo";
        ws.Cell(row, 5).Value = "Rev";
        ws.Range(row, 6, row, 8).Merge().Value = "Weld Material Data";
        ws.Cell(row, 9).Value = "Engineer/Supervisor";
        ws.Cell(row, 10).Value = "Date";
        BoldRow(ws, row, wrap: true);
        row++;

        // Row 2: FMP/FWPS values + Process + QA header
        ws.Cell(row, 1).Value = "NA";
        ws.Cell(row, 2).Value = j.WpsNo;
        ws.Cell(row, 3).Value = j.Rev;
        ws.Cell(row, 4).Value = "Nil";
        ws.Cell(row, 5).Value = "Nil";
        ws.Cell(row, 6).Value = "Process"; ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = V(1, x => x.Process);
        ws.Cell(row, 8).Value = V(2, x => x.Process);
        ws.Cell(row, 9).Value = r.EngineerSupervisor;
        ws.Cell(row, 10).Value = dateStr;
        row++;

        // Row 3: Heat No left + Size + QA Inspector header/value
        ws.Cell(row, 1).Value = "Heat No.\nof Part"; ws.Cell(row, 1).Style.Alignment.WrapText = true;
        ws.Cell(row, 2).Value = j.HeatNumberLeft;
        ws.Cell(row, 3).Value = "Piece S/N"; ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = "NA";
        ws.Cell(row, 6).Value = "Size(mm)"; ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = V(1, x => x.Size);
        ws.Cell(row, 8).Value = V(2, x => x.Size);
        ws.Cell(row, 9).Value = "QA Inspector"; ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 10).Value = "Date"; ws.Cell(row, 10).Style.Font.Bold = true;
        row++;

        // Row 4: Type + Welder header
        ws.Cell(row, 6).Value = "Type"; ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = V(1, x => x.Type);
        ws.Cell(row, 8).Value = V(2, x => x.Type);
        ws.Cell(row, 9).Value = r.QaInspector;
        row++;

        // Row 5: Heat No right + Manuf + Welder value
        ws.Cell(row, 1).Value = "Heat No.\nof Part"; ws.Cell(row, 1).Style.Alignment.WrapText = true;
        ws.Cell(row, 2).Value = j.HeatNumberRight;
        ws.Cell(row, 3).Value = "Piece S/N"; ws.Cell(row, 3).Style.Font.Bold = true;
        ws.Cell(row, 5).Value = "NA";
        ws.Cell(row, 6).Value = "Manuf."; ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = V(1, x => x.Manuf);
        ws.Cell(row, 8).Value = V(2, x => x.Manuf);
        ws.Cell(row, 9).Value = "Welder"; ws.Cell(row, 9).Style.Font.Bold = true;
        ws.Cell(row, 10).Value = "Date"; ws.Cell(row, 10).Style.Font.Bold = true;
        row++;

        // Row 6: Heat/Lot + Welder name(ID)
        ws.Cell(row, 6).Value = "Heat/Lot"; ws.Cell(row, 6).Style.Font.Bold = true;
        ws.Cell(row, 7).Value = V(1, x => x.HeatLot);
        ws.Cell(row, 8).Value = V(2, x => x.HeatLot);
        ws.Cell(row, 9).Value = welder;
        ws.Cell(row, 10).Value = dateStr;
        row++;

        // Joint description row
        ws.Cell(row, 1).Value = "Joint Description"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 2, row, 10).Merge().Value =
            $"Joining of {j.PartDescLeft} with {j.PartDescRight}";
        row++;

        return row;
    }

    private static void BoldRow(IXLWorksheet ws, int row, bool wrap = false)
    {
        for (int c = 1; c <= 10; c++)
        {
            ws.Cell(row, c).Style.Font.Bold = true;
            if (wrap) ws.Cell(row, c).Style.Alignment.WrapText = true;
        }
    }

    // Label on the top row, value on the next row (matches the boxed header cells).
    private static void LabelValueStacked(IXLWorksheet ws, int row, int col, int span, string label, string? value)
    {
        if (span > 1)
        {
            var l = ws.Range(row, col, row, col + span - 1).Merge();
            l.Value = label;
            l.Style.Font.Bold = true;
            ws.Range(row + 1, col, row + 1, col + span - 1).Merge().Value = value ?? "";
        }
        else
        {
            ws.Cell(row, col).Value = label;
            ws.Cell(row, col).Style.Font.Bold = true;
            ws.Cell(row + 1, col).Value = value ?? "";
        }
    }
}