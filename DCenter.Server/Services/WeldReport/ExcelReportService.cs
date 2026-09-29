using ClosedXML.Excel;
using DCenter.Server.Entities;

namespace DCenter.Server.Services;

public class ExcelReportService
{
    private static readonly string EmersonLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Emerson.png");
    private static readonly string FisherLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Fisher.png");

    // 3.29 characters renders as 28px (21pt) per column, so 25 columns fit A4 portrait at 100% in Excel.
    // Fit-to-width only ever scales down, so the joint page breaks stay valid wherever the sheet prints.
    private const double ColumnWidth = 3.29;

    internal byte[] Generate(Report r, ResolvedSignOff signOff)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Weld Order Card");
        ws.Style.Font.FontName = "Arial";
        ws.Style.Font.FontSize = 8;
        ws.Columns(1, WeldCardLayout.Columns).Width = ColumnWidth;

        var row = Banner(ws);

        var header = WeldCardLayout.Header(r);
        row = Draw(ws, row, header);

        var weldDate = WeldCardLayout.FormatDate(r.DateWelded);
        var joints = r.Joints.OrderBy(x => x.JointNumber)
            .Select(j => WeldCardLayout.Joint(j, weldDate, signOff))
            .ToList();
        var pageStarts = WeldCardLayout.PageStarts(
            WeldCardLayout.BannerHeight + header.Height, joints.Select(s => s.Height)).ToHashSet();

        for (var i = 0; i < joints.Count; i++)
        {
            if (pageStarts.Contains(i)) ws.PageSetup.AddHorizontalPageBreak(row - 1);
            row = Draw(ws, row, joints[i]);
        }

        PageSetup(ws, row - 1);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static int Banner(IXLWorksheet ws)
    {
        var row = 1;
        ws.Row(row).Height = WeldCardLayout.LogoRowHeight;
        if (File.Exists(EmersonLogo))
            ws.AddPicture(EmersonLogo).MoveTo(ws.Cell(row, 1), 2, 2).WithSize(150, 42);
        if (File.Exists(FisherLogo))
            ws.AddPicture(FisherLogo).MoveTo(ws.Cell(row, 22), 30, 5).WithSize(80, 34);
        row++;

        foreach (var line in WeldCardLayout.CompanyLines)
        {
            ws.Row(row).Height = WeldCardLayout.AddressRowHeight;
            var address = ws.Range(row, 10, row, WeldCardLayout.Columns).Merge();
            address.Value = line;
            address.Style.Font.FontSize = 7;
            address.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            row++;
        }

        ws.Row(row).Height = WeldCardLayout.TitleRowHeight;
        var title = ws.Range(row, 1, row, WeldCardLayout.Columns).Merge();
        title.Value = WeldCardLayout.Title;
        title.Style.Font.Bold = true;
        title.Style.Font.FontSize = 12;
        title.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        title.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        return row + 1;
    }

    private static int Draw(IXLWorksheet ws, int top, GridSection section)
    {
        for (var i = 0; i < section.RowHeights.Count; i++)
            ws.Row(top + i).Height = section.RowHeights[i];

        var block = ws.Range(top, 1, top + section.RowHeights.Count - 1, WeldCardLayout.Columns);
        block.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        block.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        block.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

        foreach (var c in section.Cells)
        {
            var range = ws.Range(top + c.Row - 1, c.Col, top + c.Row + c.RowSpan - 2, c.Col + c.ColSpan - 1);
            if (c.RowSpan > 1 || c.ColSpan > 1) range.Merge();
            range.FirstCell().Value = c.Text;

            var style = range.Style;
            style.Alignment.Horizontal = c.Center ? XLAlignmentHorizontalValues.Center : XLAlignmentHorizontalValues.Left;
            switch (c.Style)
            {
                case GridStyle.Label:
                    style.Font.Bold = true;
                    style.Alignment.WrapText = true;
                    break;
                case GridStyle.Value:
                    style.Alignment.ShrinkToFit = true;
                    break;
                case GridStyle.Text:
                    style.Alignment.WrapText = true;
                    break;
                case GridStyle.Note:
                    style.Font.Bold = true;
                    style.Font.FontSize = 7;
                    style.Alignment.WrapText = true;
                    break;
            }
        }

        return top + section.RowHeights.Count;
    }

    private static void PageSetup(IXLWorksheet ws, int lastRow)
    {
        var p = ws.PageSetup;
        p.PaperSize = XLPaperSize.A4Paper;
        p.PageOrientation = XLPageOrientation.Portrait;
        p.FitToPages(1, 0);
        p.Margins.Top = 0.4;
        p.Margins.Bottom = 0.6;
        p.Margins.Left = 0.4;
        p.Margins.Right = 0.4;
        p.Margins.Header = 0.2;
        p.Margins.Footer = 0.3;
        p.CenterHorizontally = true;
        p.PrintAreas.Add(1, 1, lastRow, WeldCardLayout.Columns);
        p.Footer.Left.AddText(WeldCardLayout.FormNumber);
        p.Footer.Right.AddText("Page ");
        p.Footer.Right.AddText(XLHFPredefinedText.PageNumber);
        p.Footer.Right.AddText(" of ");
        p.Footer.Right.AddText(XLHFPredefinedText.NumberOfPages);
    }
}
