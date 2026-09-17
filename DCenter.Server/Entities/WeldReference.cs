namespace DCenter.Server.Entities;

// Source table: MRN_NO
public class MrnSpec
{
    public int Id { get; set; }
    public string Mrn { get; set; } = string.Empty;
    public string? Form { get; set; }
    public string? FullSpecification { get; set; }
    public string SpecNo { get; set; } = string.Empty;
}

// Source table: BPVC_IX
public class BpvcMaterial
{
    public int Id { get; set; }
    public string SpecNo { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? UnsNo { get; set; }
    public string? MinTensile { get; set; }
    public string PNo { get; set; } = string.Empty;
    public string? GroupNo { get; set; }
    public string? IsoGroup { get; set; }
    public string? BrazingPNo { get; set; }
    public string? NominalComposition { get; set; }
    public string? TypicalProductForm { get; set; }
    public string? NominalThicknessLimits { get; set; }
}