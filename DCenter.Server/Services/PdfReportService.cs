using DCenter.Server.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DCenter.Server.Services;

// Renders the on-screen PDF (Image 1: "WELD SHOP JOB REPORT").
public class PdfReportService
{
    private static readonly string LogoPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "emerson.png");

    public byte[] Generate(Report r)
    {
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(t => t.FontSize(8).FontFamily(Fonts.Arial));

                page.Content().Column(col =>
                {
                    col.Spacing(6);
                    col.Item().AlignCenter().Text("WELD SHOP  JOB REPORT")
                       .Bold().FontSize(12).Underline();

                    col.Item().Element(e => Header(e, r));

                    foreach (var joint in r.Joints.OrderBy(j => j.JointNumber))
                        col.Item().Element(e => JointBlock(e, joint));
                });

                page.Footer().AlignRight().Text("F-WD-005 (Rev : 00)").FontSize(7);
            });
        }).GeneratePdf();
    }

    private static void Header(IContainer c, Report r)
    {
        c.Row(row =>
        {
            row.RelativeItem().Column(left =>
            {
                left.Item().Element(e => Field(e, "Report Required?", r.ReportRequired ? "YES" : "NO"));
                left.Item().Element(e => Field(e, "Date Welded :", r.DateWelded?.ToString("d/M/yyyy") ?? "-"));
                left.Item().Element(e => Field(e, "Work Order:", r.WorkOrder));
                left.Item().Element(e => Field(e, "Part No. :", r.PartNo));
                left.Item().Element(e => Field(e, "Description:", r.Description));
            });

            row.ConstantItem(20);

            row.RelativeItem().Table(t =>
            {
                t.ColumnsDefinition(d =>
                {
                    d.RelativeColumn(2);
                    d.RelativeColumn();
                    d.RelativeColumn();
                    d.RelativeColumn();
                });
                Cell(t, "", true); Cell(t, "1", true); Cell(t, "2", true); Cell(t, "3", true);
                Cell(t, "Material Spec:", true);
                Cell(t, r.MaterialSpec1); Cell(t, r.MaterialSpec2); Cell(t, r.MaterialSpec3);
                Cell(t, "Grade:", true);
                Cell(t, r.Grade1); Cell(t, r.Grade2); Cell(t, r.Grade3);
                Cell(t, "P#:", true);
                Cell(t, r.PNumber1); Cell(t, r.PNumber2); Cell(t, r.PNumber3);
            });
        });
    }

    private static void JointBlock(IContainer c, Joint j)
    {
        c.Column(col =>
        {
            col.Item().PaddingTop(4).Text($"Joint {j.JointNumber}").Bold().FontSize(10);
            col.Item().Row(row =>
            {
                row.RelativeItem().Table(t =>
                {
                    t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(2); });
                    Cell(t, "Part Desc. :", true); Cell(t, j.PartDescLeft);
                    Cell(t, "Part No. :", true); Cell(t, j.PartNoLeft);
                    Cell(t, "Heat Number :", true); Cell(t, j.HeatNumberLeft);
                    Cell(t, "WPS No.:", true); Cell(t, j.WpsNo);
                    Cell(t, "Welder Name :", true); Cell(t, j.WelderName);
                });
                row.ConstantItem(8);
                row.RelativeItem().Table(t =>
                {
                    t.ColumnsDefinition(d => { d.RelativeColumn(); d.RelativeColumn(2); });
                    Cell(t, "Part Desc. :", true); Cell(t, j.PartDescRight);
                    Cell(t, "Part No. :", true); Cell(t, j.PartNoRight);
                    Cell(t, "Heat Number :", true); Cell(t, j.HeatNumberRight);
                    Cell(t, "Rev:", true); Cell(t, j.Rev);
                    Cell(t, "Welder No", true); Cell(t, j.WelderNo);
                });
                row.ConstantItem(8);
                row.RelativeItem().Element(e => ElectrodeTable(e, j));
            });
        });
    }

    private static void ElectrodeTable(IContainer c, Joint j)
    {
        var m = j.Materials.OrderBy(x => x.ColumnNumber).ToList();
        string V(int col, Func<JointMaterial, string?> pick)
            => m.FirstOrDefault(x => x.ColumnNumber == col) is { } hit ? pick(hit) ?? "-" : "-";

        c.Table(t =>
        {
            t.ColumnsDefinition(d =>
            {
                d.RelativeColumn(1.4f); d.RelativeColumn(); d.RelativeColumn(); d.RelativeColumn();
            });
            Cell(t, "Electrode Data:", true); Cell(t, "GTAW", true); Cell(t, "-", true); Cell(t, "-", true);
            Cell(t, "Process", true); Cell(t, V(1, x => x.Process)); Cell(t, V(2, x => x.Process)); Cell(t, V(3, x => x.Process));
            Cell(t, "Size", true); Cell(t, V(1, x => x.Size)); Cell(t, V(2, x => x.Size)); Cell(t, V(3, x => x.Size));
            Cell(t, "Type", true); Cell(t, V(1, x => x.Type)); Cell(t, V(2, x => x.Type)); Cell(t, V(3, x => x.Type));
            Cell(t, "Manuf", true); Cell(t, V(1, x => x.Manuf)); Cell(t, V(2, x => x.Manuf)); Cell(t, V(3, x => x.Manuf));
            Cell(t, "Heat/Lot", true); Cell(t, V(1, x => x.HeatLot)); Cell(t, V(2, x => x.HeatLot)); Cell(t, V(3, x => x.HeatLot));
        });
    }

    private static void Field(IContainer c, string label, string? value)
    {
        c.Row(row =>
        {
            row.ConstantItem(90).Text(label).SemiBold();
            row.RelativeItem().BorderBottom(0.5f).Text(value ?? "");
        });
    }

    private static void Cell(TableDescriptor t, string? text, bool header = false)
    {
        var cell = t.Cell().Border(0.5f).Padding(2);
        var txt = cell.Text(text ?? "");
        if (header) txt.SemiBold();
    }
}
