using DCenter.Server.Entities;

namespace DCenter.Server.Services;

internal enum GridStyle { Label, Value, Text, Note }

internal sealed record GridCell(
    int Row, int Col, int RowSpan, int ColSpan, string Text, GridStyle Style, bool Center = false);

internal sealed record GridSection(IReadOnlyList<double> RowHeights, IReadOnlyList<GridCell> Cells)
{
    public double Height => RowHeights.Sum();
}

// The F-WD-005 Weld Order Card as a 25-column grid, shared by the Excel and PDF exports
// so both print the same form on A4 portrait. Heights are in points.
internal static class WeldCardLayout
{
    public const int Columns = 25;
    public const string FormNumber = "F-WD-005 (Rev : 00)";
    public const string Title = "Weld Order Card";

    public static readonly string[] CompanyLines =
    [
        "Emerson Process Management Manufacturing (M) Sdn Bhd",
        "Lot 13111, Mukim Labu Kawasan Perindustrian Labu",
        "71807 Nilai, Negeri Sembilan",
        "Tel: +60-6-795 2828",
    ];

    public const double LogoRowHeight = 34;
    public const double AddressRowHeight = 10;
    public const double TitleRowHeight = 18;
    public static double BannerHeight => LogoRowHeight + CompanyLines.Length * AddressRowHeight + TitleRowHeight;

    // A4 portrait (841.9pt) less 0.4in top and 0.6in bottom margins, with a little slack for rounding.
    public const double PrintableHeight = 760;

    private const string Instruction =
        "RECORD HEAT NO. PIECE SERIAL NO. AND WELD MATERIAL FOR EACH WELD JOINT/REPAIR. "
        + "RECORD WELD JOINT NUMBER(S) IF HEAT NO. OR PIECE SERIAL NO. IS NOT REQUIRED";

    public static string FormatDate(DateOnly? date) => date?.ToString("d/M/yyyy") ?? "";

    public static string PartLabel(string? description, string? partNo)
    {
        var desc = description?.Trim() ?? "";
        var no = partNo?.Trim() ?? "";
        if (no.Length == 0) return desc.Length == 0 ? "-" : desc;
        return desc.Length == 0 ? no : $"{desc} ({no})";
    }

    public static GridSection Header(Report r)
    {
        var cells = new List<GridCell>
        {
            L(1, 1, 1, 4, "Part No."), T(2, 1, 1, 4, r.PartNo),
            L(1, 5, 1, 8, "Part Description"), T(2, 5, 1, 8, r.Description),
            L(1, 13, 1, 4, "Work Order No."), T(2, 13, 1, 4, r.WorkOrderNumber),
            L(1, 17, 1, 5, "Piece S/N"), T(2, 17, 1, 5, "-"),
            L(1, 22, 1, 4, "Date"), T(2, 22, 1, 4, FormatDate(r.DateWelded)),

            L(3, 1, 1, 4, "MRP/CSP No."), V(4, 1, 2, 4, "-"),
            L(3, 5, 3, 2, "Material Welded", center: true),
            new(3, 18, 3, 8, Instruction, GridStyle.Note),
        };

        string?[] specs = [r.MaterialSpec1, r.MaterialSpec2, r.MaterialSpec3];
        string?[] grades = [r.Grade1, r.Grade2, r.Grade3];
        string?[] pnums = [r.PNumber1, r.PNumber2, r.PNumber3];
        for (var i = 0; i < 3; i++)
        {
            var row = 3 + i;
            cells.Add(L(row, 7, 1, 1, "Spec"));
            cells.Add(V(row, 8, 1, 3, specs[i]));
            cells.Add(L(row, 11, 1, 2, "Grade"));
            cells.Add(V(row, 13, 1, 3, grades[i]));
            cells.Add(L(row, 16, 1, 1, "P#"));
            cells.Add(V(row, 17, 1, 1, pnums[i]));
        }

        return new([12, 20, 16, 16, 16], cells);
    }

    // One joint is 8 rows: block header (2), FMP/Process, two heat-number pairs, joint description.
    // The last heat-number pair is taller so a long welder name fits on three lines.
    public static GridSection Joint(Joint j, string weldDate, ResolvedSignOff signOff)
    {
        var m = j.Materials.OrderBy(x => x.ColumnNumber).ToList();
        string M(int col, Func<JointMaterial, string?> pick)
            => m.FirstOrDefault(x => x.ColumnNumber == col) is { } hit && !string.IsNullOrWhiteSpace(pick(hit))
                ? pick(hit)! : "-";

        var (welder, welderDate) = signOff.WelderFor(j) ?? (ReportSignOffRules.WelderDisplay(j.WelderName, j.WelderNo), weldDate);

        var cells = new List<GridCell>
        {
            L(1, 1, 2, 4, "Fab No./Repair NCR-DVR No", center: true),
            L(1, 5, 2, 4, "FMP/FWPS No.", center: true),
            L(1, 9, 2, 1, "Rev", center: true),
            L(1, 10, 2, 2, "Amend. No", center: true),
            L(1, 12, 2, 1, "Rev", center: true),
            L(1, 13, 2, 8, "Weld Material Data", center: true),
            L(1, 21, 1, 3, "Engineer/ Supervisor"), L(1, 24, 1, 2, "Date"),
            V(2, 21, 1, 3, signOff.EngineerName), V(2, 24, 1, 2, signOff.EngineerDate),

            V(3, 1, 1, 4, j.JointNumber.ToString(System.Globalization.CultureInfo.InvariantCulture), center: true),
            V(3, 5, 1, 4, j.WpsNo, center: true),
            V(3, 9, 1, 1, j.Rev, center: true),
            V(3, 10, 1, 2, "Nil", center: true),
            V(3, 12, 1, 1, "Nil", center: true),
            L(3, 21, 1, 3, "QA Inspector"), L(3, 24, 1, 2, "Date"),
            V(4, 21, 1, 3, signOff.QaName), V(4, 24, 1, 2, signOff.QaDate),
            L(5, 21, 1, 3, "Welder"), L(5, 24, 1, 2, "Date"),
            T(6, 21, 2, 3, welder), V(6, 24, 2, 2, welderDate),

            L(4, 1, 2, 2, "Heat No. of Part"), T(4, 3, 2, 4, j.HeatNumberLeft),
            L(4, 7, 2, 3, "Piece S/N", center: true), V(4, 10, 2, 3, "-", center: true),
            L(6, 1, 2, 2, "Heat No. of Part"), T(6, 3, 2, 4, j.HeatNumberRight),
            L(6, 7, 2, 3, "Piece S/N", center: true), V(6, 10, 2, 3, "-", center: true),

            L(8, 1, 1, 4, "Joint Description"),
            T(8, 5, 1, 21, $"Joining of {PartLabel(j.PartDescLeft, j.PartNoLeft)} with {PartLabel(j.PartDescRight, j.PartNoRight)}"),
        };

        (int Row, string Label, Func<JointMaterial, string?> Pick)[] materialRows =
        [
            (3, "Process", x => x.Process),
            (4, "Size(mm)", x => x.Size),
            (5, "Type", x => x.Type),
            (6, "Manuf", x => x.Manuf),
            (7, "Heat/Lot", x => x.HeatLot),
        ];
        foreach (var (row, label, pick) in materialRows)
        {
            cells.Add(L(row, 13, 1, 2, label));
            for (var col = 1; col <= 3; col++)
                cells.Add(V(row, 13 + col * 2, 1, 2, M(col, pick), center: true));
        }

        return new([20, 12, 12, 12, 12, 14, 14, 20], cells);
    }

    // Row indexes (0-based, into the joint list) that must start a new page.
    public static IReadOnlyList<int> PageStarts(double firstPageUsed, IEnumerable<double> jointHeights)
    {
        var starts = new List<int>();
        var used = firstPageUsed;
        var i = 0;
        foreach (var h in jointHeights)
        {
            if (used + h > PrintableHeight && i > 0)
            {
                starts.Add(i);
                used = 0;
            }
            used += h;
            i++;
        }
        return starts;
    }

    private static GridCell L(int row, int col, int rowSpan, int colSpan, string text, bool center = false)
        => new(row, col, rowSpan, colSpan, text, GridStyle.Label, center);

    private static GridCell V(int row, int col, int rowSpan, int colSpan, string? text, bool center = false)
        => new(row, col, rowSpan, colSpan, text ?? "", GridStyle.Value, center);

    private static GridCell T(int row, int col, int rowSpan, int colSpan, string? text)
        => new(row, col, rowSpan, colSpan, text ?? "", GridStyle.Text);
}
