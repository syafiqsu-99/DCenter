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
        for (int c = 1; c <= 25; c++) ws.Column(c).Width = 5;

        var dateStr = r.DateWelded?.ToString("d/M/yyyy") ?? "";
        int row = 1;

        // ---- Logos + company header ----
        if (File.Exists(EmersonLogo))
            ws.AddPicture(EmersonLogo).MoveTo(ws.Cell(row, 1)).WithSize(150, 42);
        if (File.Exists(FisherLogo))
            ws.AddPicture(FisherLogo).MoveTo(ws.Cell(row, 25)).WithSize(80, 34);
        row += 1;

        ws.Cell(row + 1, 18).Value = "Emerson Process Management Manufacturing (M) Sdn Bhd";
        ws.Cell(row + 2, 18).Value = "Lot 13111, Mukim Labu Kawasan Perindustrian Labu";
        ws.Cell(row + 3, 18).Value = "71807 Nilai, Negeri Sembilan";
        ws.Cell(row + 4, 18).Value = "Tel: +60-6-795 2828";
        for (int i = 1; i <= 4; i++) ws.Cell(row + i, 18).Style.Font.FontSize = 8;
        row += 5;

        var title = ws.Range(row, 1, row, 25).Merge();
        title.Value = "Weld Order Card";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 12;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row ++;

        int gridTop = row;

        // ---- Header block: Part No / Desc / Job No / Piece S/N / Date ----
        LabelValueStacked(ws, row, 1, 4, "Part No.", r.PartNo);
        LabelValueStacked(ws, row, 5, 8, "Part Description", r.Description);
        LabelValueStacked(ws, row, 13, 4, "Work Order No.", r.WorkOrderNumber);
        LabelValueStacked(ws, row, 17, 5, "Piece S/N", "-");
        LabelValueStacked(ws, row, 22, 4, "Date", dateStr);
        row += 2;

        // ---- MRP/CSP + Material Welded (spec/grade/P# rows 1..3) + instruction ----
        var mrpLabel = ws.Range(row, 1, row, 4).Merge();
        mrpLabel.Value = "MRP/CSP No.";
        mrpLabel.Style.Font.Bold = true;
        mrpLabel.Style.Alignment.WrapText = true;
        ws.Range(row + 1, 1, row + 2, 4).Merge();
        // Material Welded label spans the three spec rows (col 2)
        var matLabel = ws.Range(row, 5, row + 2, 6).Merge();
        matLabel.Value = "Material Welded";
        matLabel.Style.Font.Bold = true;
        matLabel.Style.Alignment.WrapText = true;
        matLabel.Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        matLabel.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);

        // three spec/grade/P# rows
        string[] specs = { r.MaterialSpec1 ?? "", r.MaterialSpec2 ?? "", r.MaterialSpec3 ?? "" };
        string[] grades = { r.Grade1 ?? "", r.Grade2 ?? "", r.Grade3 ?? "" };
        string[] pnums = { r.PNumber1 ?? "", r.PNumber2 ?? "", r.PNumber3 ?? "" };
        for (int i = 0; i < 3; i++)
        {
            int rr = row + i;
            ws.Cell(rr, 7).Value = "Spec"; ws.Cell(rr, 7).Style.Font.Bold = true;
            ws.Range(rr, 8, rr, 9).Merge().Value = specs[i];
            ws.Cell(rr, 10).Value = "Grade"; ws.Cell(rr, 10).Style.Font.Bold = true;
            ws.Range(rr, 11, rr, 12).Merge().Value = grades[i];
            ws.Cell(rr, 13).Value = "P#"; ws.Cell(rr, 13).Style.Font.Bold = true;
            ws.Range(rr, 14, rr, 15).Merge().Value = pnums[i];
        }

        var instr = ws.Range(row, 17, row + 2, 25).Merge();
        instr.Value = "RECORD HEAT NO. PIECE SERIAL NO. AND WELD MATERIAL FOR "
                    + "EACH WELD JOINT/REPAIR. RECORD WELD JOINT NUMBER(S) IF "
                    + "HEAT NO. OR PIECE SERIAL NO. IS NOT REQUIRED";
        instr.Style.Font.Bold = true;
        instr.Style.Alignment.WrapText = true;
        instr.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        row += 3;

        // ---- Joint blocks ----
        foreach (var j in r.Joints.OrderBy(x => x.JointNumber))
            row = JointBlock(ws, row, j, r, dateStr);

        // ---- Footer ----
        row++;
        ws.Cell(row, 1).Value = "F-WD-005 (Rev : 00)";
        ws.Cell(row, 1).Style.Font.Bold = true;

        // Borders across the whole grid.
        var used = ws.Range(gridTop, 1, row - 2, 25);
        used.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        used.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        used.Style.Alignment.SetWrapText();

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
        ws.Range(row, 1, row + 1, 4).Merge().Value = "Fab No./Repair NCR-DVR No";
        ws.Range(row, 5, row + 1, 7).Merge().Value = "FMP/FWPS No.";
        ws.Range(row, 8, row + 1, 9).Merge().Value = "Rev";
        ws.Range(row, 10, row + 1, 10).Merge().Value = "Amend. No";
        ws.Range(row, 11, row + 1, 12).Merge().Value = "Rev";
        ws.Range(row, 13, row + 1, 20).Merge().Value = "Weld Material Data";
        ws.Range(row, 21, row, 23).Merge().Value = "Engineer/Supervisor";
        ws.Range(row, 24, row, 25).Merge().Value = "Date";
        ws.Range(row + 1, 21, row + 1, 23).Merge().Value = r.EngineerSupervisor;
        ws.Range(row + 1, 24, row + 1, 25).Merge().Value = dateStr;
        ws.Range(row, 1, row + 1, 20).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        BoldRow(ws, row, wrap: true);
        row += 2;

        // Row 2: FMP/FWPS values + Process + QA header
        ws.Range(row, 1, row, 4).Merge().Value = "NA";
        ws.Range(row, 5, row, 7).Merge().Value = j.WpsNo;
        ws.Range(row, 8, row, 9).Merge().Value = j.Rev;
        ws.Range(row, 10, row, 10).Merge().Value = "Nil";
        ws.Range(row, 11, row, 12).Merge().Value = "Nil";
        ws.Range(row, 13, row, 14).Merge().Value = "Process"; ws.Cell(row, 13).Style.Font.Bold = true;
        ws.Range(row, 15, row, 16).Merge().Value = V(1, x => x.Process);
        ws.Range(row, 17, row, 18).Merge().Value = V(2, x => x.Process);
        ws.Range(row, 19, row, 20).Merge().Value = V(3, x => x.Process);
        ws.Range(row, 21, row, 23).Merge().Value = "QA Inspector"; ws.Cell(row, 21).Style.Font.Bold = true;
        ws.Range(row, 24, row, 25).Merge().Value = "Date"; ws.Cell(row, 24).Style.Font.Bold = true;
        row++;

        // Row 3: Heat No left + Size + QA Inspector header/value
        ws.Range(row, 1, row + 1, 2).Merge().Value = "Heat No. of Part"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 3, row + 1, 6).Merge().Value = j.HeatNumberLeft;
        ws.Range(row, 7, row + 1, 9).Merge().Value = "Piece S/N"; ws.Cell(row, 7).Style.Font.Bold = true; ws.Cell(row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center); ws.Cell(row, 7).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Range(row, 10, row + 1, 12).Merge().Value = "NA";
        ws.Range(row, 13, row, 14).Merge().Value = "Size(mm)"; ws.Cell(row, 13).Style.Font.Bold = true;
        ws.Range(row, 15, row, 16).Merge().Value = V(1, x => x.Size);
        ws.Range(row, 17, row, 18).Merge().Value = V(2, x => x.Size);
        ws.Range(row, 19, row, 20).Merge().Value = V(3, x => x.Size);
        ws.Range(row + 1, 13, row + 1, 14).Merge().Value = "Type"; ws.Cell(row + 1, 13).Style.Font.Bold = true;
        ws.Range(row + 1, 15, row + 1, 16).Merge().Value = V(1, x => x.Type);
        ws.Range(row + 1, 17, row + 1, 18).Merge().Value = V(2, x => x.Type);
        ws.Range(row + 1, 19, row + 1, 20).Merge().Value = V(3, x => x.Type);
        ws.Range(row, 21, row, 23).Merge().Value = r.QaInspector;
        ws.Range(row, 24, row, 25).Merge().Value = dateStr;
        ws.Range(row + 1, 21, row + 1, 23).Merge().Value = "Welder"; ws.Cell(row + 1, 21).Style.Font.Bold = true;
        ws.Range(row + 1, 24, row + 1, 25).Merge().Value = "Date"; ws.Cell(row + 1, 24).Style.Font.Bold = true;
        row += 2;

        // Row 5: Heat No right + Manuf + Welder value
        ws.Range(row, 1, row + 1, 2).Merge().Value = "Heat No.of Part"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 3, row + 1, 6).Merge().Value = j.HeatNumberRight;
        ws.Range(row, 7, row + 1, 9).Merge().Value = "Piece S/N"; ws.Cell(row, 7).Style.Font.Bold = true; ws.Cell(row, 7).Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center); ws.Cell(row, 7).Style.Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        ws.Range(row, 10, row + 1, 12).Merge().Value = "NA";
        ws.Range(row, 13, row, 14).Merge().Value = "Manuf"; ws.Cell(row, 13).Style.Font.Bold = true;
        ws.Range(row, 15, row, 16).Merge().Value = V(1, x => x.Manuf);
        ws.Range(row, 17, row, 18).Merge().Value = V(2, x => x.Manuf);
        ws.Range(row, 19, row, 20).Merge().Value = V(3, x => x.Manuf);
        ws.Range(row + 1, 13, row + 1, 14).Merge().Value = "Heat/Lot"; ws.Cell(row + 1, 13).Style.Font.Bold = true;
        ws.Range(row + 1, 15, row + 1, 16).Merge().Value = V(1, x => x.HeatLot);
        ws.Range(row + 1, 17, row + 1, 18).Merge().Value = V(2, x => x.HeatLot);
        ws.Range(row + 1, 19, row + 1, 20).Merge().Value = V(3, x => x.HeatLot);
        ws.Range(row, 21, row, 23).Merge().Value = welder;
        ws.Range(row, 24, row, 25).Merge().Value = dateStr;
        row += 2;

        // Joint description row
        ws.Range(row, 1, row, 4).Merge().Value = "Joint Description"; ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 5, row, 25).Merge().Value =
            $"Joining of {j.PartDescLeft} with {j.PartDescRight}";
        row++;

        return row;
    }

    private static void BoldRow(IXLWorksheet ws, int row, bool wrap = false)
    {
        for (int c = 1; c <= 25; c++)
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