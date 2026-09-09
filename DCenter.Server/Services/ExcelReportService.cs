using ClosedXML.Excel;
using DCenter.Server.Entities;

namespace DCenter.Server.Services;

// Renders the downloadable Excel (Image 2: Emerson "Weld Order Card", F-WD-005).
public class ExcelReportService
{
    private static readonly string EmersonLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "emerson.png");
    private static readonly string FisherLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "fisher.png");

    public byte[] Generate(Report r)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Weld Order Card");
        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 8;
        ws.ColumnsUsed().Width = 12;

        int row = 1;

        // --- Logos ---
        if (File.Exists(EmersonLogo))
            ws.AddPicture(EmersonLogo).MoveTo(ws.Cell(row, 1)).WithSize(150, 40);
        if (File.Exists(FisherLogo))
            ws.AddPicture(FisherLogo).MoveTo(ws.Cell(row, 9)).WithSize(90, 40);
        row += 3;

        ws.Cell(row, 5).Value = "Emerson Process Management Manufacturing (M) Sdn Bhd";
        row += 1;
        ws.Cell(row, 5).Value = "Lot 13111, Mukim Labu Kawasan Perindustrian Labu, 71807 Nilai, Negeri Sembilan";
        row += 2;

        var title = ws.Range(row, 1, row, 10).Merge();
        title.Value = "Weld Order Card";
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 12;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        row += 2;

        // --- Header grid ---
        HeaderPair(ws, row, 1, "Part No.", r.PartNo);
        HeaderPair(ws, row, 3, "Part Description", r.Description);
        HeaderPair(ws, row, 5, "Job No.", r.JobNumber);
        HeaderPair(ws, row, 7, "Piece S/N", "-");
        HeaderPair(ws, row, 9, "Date", r.DateWelded?.ToString("d/M/yyyy"));
        row += 2;

        // Material Welded block (specs 1..3)
        ws.Cell(row, 1).Value = "Material Welded";
        ws.Cell(row, 2).Value = "Spec"; ws.Cell(row, 3).Value = r.MaterialSpec1;
        ws.Cell(row, 4).Value = "Grade"; ws.Cell(row, 5).Value = r.Grade1;
        ws.Cell(row, 6).Value = "P#"; ws.Cell(row, 7).Value = r.PNumber1;
        row++;
        ws.Cell(row, 2).Value = "Spec"; ws.Cell(row, 3).Value = r.MaterialSpec2;
        ws.Cell(row, 4).Value = "Grade"; ws.Cell(row, 5).Value = r.Grade2;
        ws.Cell(row, 6).Value = "P#"; ws.Cell(row, 7).Value = r.PNumber2;
        row++;
        ws.Cell(row, 2).Value = "Spec"; ws.Cell(row, 3).Value = r.MaterialSpec3;
        ws.Cell(row, 4).Value = "Grade"; ws.Cell(row, 5).Value = r.Grade3;
        ws.Cell(row, 6).Value = "P#"; ws.Cell(row, 7).Value = r.PNumber3;

        var instr = ws.Range(row - 2, 8, row, 10).Merge();
        instr.Value = "RECORD HEAT NO. PIECE SERIAL NO. AND WELD MATERIAL FOR "
                    + "EACH WELD JOINT/REPAIR. RECORD WELD JOINT NUMBER(S) IF "
                    + "HEAT NO. OR PIECE SERIAL NO. IS NOT REQUIRED";
        instr.Style.Font.Bold = true;
        instr.Style.Alignment.WrapText = true;
        row += 2;

        // --- Joint blocks ---
        foreach (var j in r.Joints.OrderBy(x => x.JointNumber))
            row = JointBlock(ws, row, j, r);

        ws.Cell(row + 1, 1).Value = "F-WD-005 (Rev : 00)";

        ws.Columns().AdjustToContents(8d, 40d);
        ws.RangeUsed()?.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static int JointBlock(IXLWorksheet ws, int row, Joint j, Report r)
    {
        ws.Cell(row, 1).Value = "Fab No./Repair NCR-DVR No";
        ws.Cell(row, 2).Value = "FMP/FWPS No.";
        ws.Cell(row, 3).Value = "Rev";
        ws.Cell(row, 4).Value = "Amend. No";
        ws.Cell(row, 5).Value = "Rev";
        ws.Range(row, 6, row, 8).Merge().Value = "Weld Material Data";
        ws.Cell(row, 9).Value = "Engineer/Supervisor";
        ws.Cell(row, 10).Value = r.EngineerSupervisor;
        row++;

        var m = j.Materials.OrderBy(x => x.ColumnNumber).ToList();
        string V(int col, Func<JointMaterial, string?> pick)
            => m.FirstOrDefault(x => x.ColumnNumber == col) is { } hit ? pick(hit) ?? "-" : "-";

        ws.Cell(row, 1).Value = "NA";
        ws.Cell(row, 2).Value = j.WpsNo;
        ws.Cell(row, 3).Value = j.Rev;
        ws.Cell(row, 4).Value = "Nil";
        ws.Cell(row, 5).Value = "Nil";
        ws.Cell(row, 6).Value = "Process";
        ws.Cell(row, 7).Value = V(1, x => x.Process);
        ws.Cell(row, 8).Value = V(2, x => x.Process);
        ws.Cell(row, 9).Value = "QA Inspector";
        ws.Cell(row, 10).Value = r.QaInspector;
        row++;

        ws.Cell(row, 1).Value = "Heat No. of Part";
        ws.Cell(row, 2).Value = j.HeatNumberLeft;
        ws.Cell(row, 3).Value = "Piece S/N";
        ws.Cell(row, 5).Value = "NA";
        ws.Cell(row, 6).Value = "Size(mm)";
        ws.Cell(row, 7).Value = V(1, x => x.Size);
        ws.Cell(row, 9).Value = "Welder";
        ws.Cell(row, 10).Value = string.IsNullOrWhiteSpace(j.WelderName)
            ? null : $"{j.WelderName} (ID {j.WelderNo})";
        row++;

        ws.Cell(row, 6).Value = "Type"; ws.Cell(row, 7).Value = V(1, x => x.Type);
        row++;
        ws.Cell(row, 1).Value = "Heat No. of Part";
        ws.Cell(row, 2).Value = j.HeatNumberRight;
        ws.Cell(row, 3).Value = "Piece S/N";
        ws.Cell(row, 6).Value = "Manuf."; ws.Cell(row, 7).Value = V(1, x => x.Manuf);
        row++;
        ws.Cell(row, 6).Value = "Heat/Lot"; ws.Cell(row, 7).Value = V(1, x => x.HeatLot);
        row++;

        ws.Cell(row, 1).Value = "Joint Description";
        ws.Range(row, 2, row, 8).Merge().Value =
            $"Joining of {j.PartDescLeft} with {j.PartDescRight}";
        row += 2;
        return row;
    }

    private static void HeaderPair(IXLWorksheet ws, int row, int col, string label, string? value)
    {
        ws.Cell(row, col).Value = label;
        ws.Cell(row, col).Style.Font.Bold = true;
        ws.Cell(row + 1, col).Value = value;
    }
}
