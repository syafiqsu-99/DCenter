using DCenter.Server.Entities;
using static DCenter.Server.Services.ReferenceCsv;

namespace DCenter.Server.Services;

public sealed class BpvcTable : IReferenceTable<BpvcMaterial>
{
    public Column[] Columns { get; } =
    [
        new("SpecNo", Required: true, Key: true, "SpecNoRaw", "SpecNo") { MaxLength = 100, Label = "Spec No." },
        new("Designation", Required: false, Key: true, "Designation", "Grade", "DesignationAlloyGrade") { MaxLength = 200, Label = "Designation / Alloy / Grade" },
        new("UnsNo", Required: false, Key: true) { MaxLength = 100, Label = "UNS No." },
        new("PNo", Required: true, Key: true) { MaxLength = 50, Label = "P-No." },
        new("MinTensile", Required: false, Key: false) { MaxLength = 100, Label = "Min. Tensile" },
        new("GroupNo", Required: false, Key: false) { MaxLength = 50, Label = "Group No." },
        new("IsoGroup", Required: false, Key: false, "IsoGroup", "ISO15608Group") { MaxLength = 100, Label = "ISO 15608 Group" },
        new("BrazingPNo", Required: false, Key: false) { MaxLength = 50, Label = "Brazing P-No." },
        new("NominalComposition", Required: false, Key: false) { MaxLength = 400, Label = "Nominal Composition" },
        new("TypicalProductForm", Required: false, Key: false) { MaxLength = 200, Label = "Typical Product Form" },
        new("NominalThicknessLimits", Required: false, Key: false) { MaxLength = 200, Label = "Nominal Thickness Limits" },
    ];

    public string FileName => "bpvc.csv";
    public string RequiredMessage => "Spec No. and P-No. are required.";
    public string EntityName => "BPVC";

    public int IdOf(BpvcMaterial b) => b.Id;

    public string?[] Values(BpvcMaterial b) =>
    [
        b.SpecNo, b.Designation, b.UnsNo, b.PNo, b.MinTensile, b.GroupNo, b.IsoGroup,
        b.BrazingPNo, b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits,
    ];

    public void Write(BpvcMaterial b, string?[] v)
    {
        (b.SpecNo, b.Designation, b.UnsNo, b.PNo) = (Clean(v[0]) ?? "", Clean(v[1]), Clean(v[2]), Clean(v[3]) ?? "");
        (b.MinTensile, b.GroupNo, b.IsoGroup, b.BrazingPNo) = (Clean(v[4]), Clean(v[5]), Clean(v[6]), Clean(v[7]));
        (b.NominalComposition, b.TypicalProductForm, b.NominalThicknessLimits) = (Clean(v[8]), Clean(v[9]), Clean(v[10]));
    }

    public IQueryable<BpvcMaterial> Ordered(IQueryable<BpvcMaterial> q)
        => q.OrderBy(b => b.SpecNo).ThenBy(b => b.Designation).ThenBy(b => b.UnsNo).ThenBy(b => b.PNo);

    public IQueryable<BpvcMaterial> SameRequiredKey(IQueryable<BpvcMaterial> q, string?[] v)
    {
        var (specNo, pNo) = (Clean(v[0]), Clean(v[3]));
        return q.Where(b => b.SpecNo == specNo && b.PNo == pNo);
    }
}

public sealed class MrnTable : IReferenceTable<MrnSpec>
{
    public Column[] Columns { get; } =
    [
        new("MRN", Required: true, Key: true) { MaxLength = 100, Label = "MRN" },
        new("SpecNo", Required: true, Key: true, "SpecNoRaw", "SpecNo") { MaxLength = 100, Label = "Spec No." },
        new("Form", Required: false, Key: false) { MaxLength = 200, Label = "Form" },
        new("FullSpecification", Required: false, Key: false) { MaxLength = 400, Label = "Full Specification" },
    ];

    public string FileName => "mrn.csv";
    public string RequiredMessage => "MRN and Spec No. are required.";
    public string EntityName => "MRN";

    public int IdOf(MrnSpec m) => m.Id;

    public string?[] Values(MrnSpec m) => [m.Mrn, m.SpecNo, m.Form, m.FullSpecification];

    public void Write(MrnSpec m, string?[] v)
        => (m.Mrn, m.SpecNo, m.Form, m.FullSpecification) = (Clean(v[0]) ?? "", Clean(v[1]) ?? "", Clean(v[2]), Clean(v[3]));

    public IQueryable<MrnSpec> Ordered(IQueryable<MrnSpec> q) => q.OrderBy(m => m.Mrn).ThenBy(m => m.SpecNo);

    public IQueryable<MrnSpec> SameRequiredKey(IQueryable<MrnSpec> q, string?[] v)
    {
        var (mrn, specNo) = (Clean(v[0]), Clean(v[1]));
        return q.Where(m => m.Mrn == mrn && m.SpecNo == specNo);
    }
}

public sealed class WpsTable : IReferenceTable<WpsItem>
{
    public Column[] Columns { get; } =
    [
        new("WpsNo", Required: true, Key: true) { MaxLength = 200, Label = "WPS No." },
        new("PNo", Required: true, Key: true) { MaxLength = 50, Label = "P-No." },
        new("BaseMetal", Required: false, Key: false) { MaxLength = 200, Label = "Base Metal" },
        new("Process", Required: false, Key: false) { MaxLength = 100, Label = "Process" },
    ];

    public string FileName => "wps.csv";
    public string RequiredMessage => "WPS No. and P-No. are required.";
    public string EntityName => "WPS";

    public int IdOf(WpsItem w) => w.Id;

    public string?[] Values(WpsItem w) => [w.WpsNo, w.PNo, w.BaseMetal, w.Process];

    public void Write(WpsItem w, string?[] v)
        => (w.WpsNo, w.PNo, w.BaseMetal, w.Process) = (Clean(v[0]) ?? "", Clean(v[1]) ?? "", Clean(v[2]), Clean(v[3]));

    public IQueryable<WpsItem> Ordered(IQueryable<WpsItem> q) => q.OrderBy(w => w.WpsNo).ThenBy(w => w.PNo);

    public IQueryable<WpsItem> SameRequiredKey(IQueryable<WpsItem> q, string?[] v)
    {
        var (wpsNo, pNo) = (Clean(v[0]), Clean(v[1]));
        return q.Where(w => w.WpsNo == wpsNo && w.PNo == pNo);
    }
}
