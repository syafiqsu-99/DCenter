using DCenter.Server.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DCenter.Server.Services;

public class PdfReportService
{
    private static readonly string EmersonLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Emerson.png");
    private static readonly string FisherLogo =
        Path.Combine(AppContext.BaseDirectory, "Assets", "Fisher.png");

    private const float Border = 0.5f;
    private const float SingleLine = 10;

    internal byte[] Generate(Report r, ResolvedSignOff signOff)
    {
        var weldDate = WeldCardLayout.FormatDate(r.DateWelded);
        var joints = r.Joints.OrderBy(j => j.JointNumber)
            .Select(j => WeldCardLayout.Joint(j, weldDate, signOff))
            .ToList();

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(0.4f, Unit.Inch);
                page.MarginTop(0.4f, Unit.Inch);
                page.MarginBottom(0.3f, Unit.Inch);
                page.DefaultTextStyle(t => t.FontSize(8).FontFamily(Fonts.Arial));

                page.Content().Column(col =>
                {
                    col.Item().Element(Banner);
                    col.Item().Element(e => Draw(e, WeldCardLayout.Header(r)));
                    foreach (var joint in joints)
                        col.Item().ShowEntire().Element(e => Draw(e, joint));
                });

                page.Footer().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text(WeldCardLayout.FormNumber).Bold().FontSize(7);
                    row.RelativeItem().AlignRight().Text(t =>
                    {
                        t.DefaultTextStyle(s => s.FontSize(7));
                        t.Span("Page ");
                        t.CurrentPageNumber();
                        t.Span(" of ");
                        t.TotalPages();
                    });
                });
            });
        }).GeneratePdf();
    }

    private static void Banner(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().Height((float)WeldCardLayout.LogoRowHeight).Row(row =>
            {
                row.ConstantItem(112).AlignMiddle().Element(e => Logo(e, EmersonLogo));
                row.RelativeItem();
                row.ConstantItem(60).AlignMiddle().Element(e => Logo(e, FisherLogo));
            });
            foreach (var line in WeldCardLayout.CompanyLines)
                col.Item().Height((float)WeldCardLayout.AddressRowHeight).AlignRight().AlignMiddle()
                    .Text(line).FontSize(7);
            col.Item().Height((float)WeldCardLayout.TitleRowHeight).AlignCenter().AlignMiddle()
                .Text(WeldCardLayout.Title).Bold().FontSize(12);
        });
    }

    private static void Logo(IContainer c, string path)
    {
        if (File.Exists(path)) c.Image(path).FitArea();
    }

    // Every grid slot gets a bordered cell, so blanks print as empty boxes like the Excel sheet.
    // Cells keep the layout's fixed heights and scale long text down, as Excel's shrink-to-fit does.
    private static void Draw(IContainer c, GridSection section)
    {
        var rows = section.RowHeights.Count;
        var taken = new bool[rows + 1, WeldCardLayout.Columns + 1];
        foreach (var cell in section.Cells)
            for (var r = cell.Row; r < cell.Row + cell.RowSpan; r++)
                for (var k = cell.Col; k < cell.Col + cell.ColSpan; k++)
                    taken[r, k] = true;

        c.Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                for (var i = 0; i < WeldCardLayout.Columns; i++) d.RelativeColumn();
            });

            foreach (var cell in section.Cells)
            {
                var height = section.RowHeights.Skip(cell.Row - 1).Take(cell.RowSpan).Sum();
                t.Cell().Row((uint)cell.Row).Column((uint)cell.Col)
                    .RowSpan((uint)cell.RowSpan).ColumnSpan((uint)cell.ColSpan)
                    .Border(Border).Height((float)height)
                    .PaddingHorizontal(2).PaddingVertical(1).ScaleToFit().AlignMiddle()
                    .Element(e => Content(e, cell));
            }

            for (var r = 1; r <= rows; r++)
                for (var k = 1; k <= WeldCardLayout.Columns; k++)
                    if (!taken[r, k])
                        t.Cell().Row((uint)r).Column((uint)k).Border(Border)
                            .Height((float)section.RowHeights[r - 1]);
        });
    }

    private static void Content(IContainer c, GridCell cell)
    {
        if (cell.Center) c = c.AlignCenter();
        if (cell.Style == GridStyle.Value) c = c.Height(SingleLine);
        var text = c.Text(cell.Text);
        if (cell.Center) text.AlignCenter();
        switch (cell.Style)
        {
            case GridStyle.Label:
                text.Bold();
                break;
            case GridStyle.Note:
                text.Bold().FontSize(7);
                break;
        }
    }
}
