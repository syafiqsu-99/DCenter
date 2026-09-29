using DCenter.Server.Entities;
using Microsoft.Data.SqlClient;
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
    public string Name => "Bpvc";

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

    public SqlParameter[] RequiredKey(string?[] v)
        => [Sql.NVarChar("@SpecNo", Clean(v[0]), 100), Sql.NVarChar("@PNo", Clean(v[3]), 50)];
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
    public string Name => "Mrn";

    public int IdOf(MrnSpec m) => m.Id;

    public string?[] Values(MrnSpec m) => [m.Mrn, m.SpecNo, m.Form, m.FullSpecification];

    public void Write(MrnSpec m, string?[] v)
        => (m.Mrn, m.SpecNo, m.Form, m.FullSpecification) = (Clean(v[0]) ?? "", Clean(v[1]) ?? "", Clean(v[2]), Clean(v[3]));

    public SqlParameter[] RequiredKey(string?[] v)
        => [Sql.NVarChar("@Mrn", Clean(v[0]), 100), Sql.NVarChar("@SpecNo", Clean(v[1]), 100)];
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
    public string Name => "Wps";

    public int IdOf(WpsItem w) => w.Id;

    public string?[] Values(WpsItem w) => [w.WpsNo, w.PNo, w.BaseMetal, w.Process];

    public void Write(WpsItem w, string?[] v)
        => (w.WpsNo, w.PNo, w.BaseMetal, w.Process) = (Clean(v[0]) ?? "", Clean(v[1]) ?? "", Clean(v[2]), Clean(v[3]));

    public SqlParameter[] RequiredKey(string?[] v)
        => [Sql.NVarChar("@WpsNo", Clean(v[0]), 200), Sql.NVarChar("@PNo", Clean(v[1]), 50)];
}
