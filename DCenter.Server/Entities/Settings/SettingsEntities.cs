namespace DCenter.Server.Entities;

public class Welder
{
    public int Id { get; set; }
    public string WelderName { get; set; } = string.Empty;
    public string WelderNo { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string UsageScope { get; set; } = WelderScope.Report;
}

public static class WelderScope
{
    public const string Report = "Report";
    public const string ReportAndStock = "ReportAndStock";
    public static readonly string[] All = [Report, ReportAndStock];

    public static string? Normalize(string? raw)
        => All.FirstOrDefault(s => string.Equals(s, raw?.Trim(), StringComparison.OrdinalIgnoreCase));
}

public class LookupItem
{
    public int Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class WpsItem
{
    public int Id { get; set; }
    public string WpsNo { get; set; } = string.Empty;
    public string? BaseMetal { get; set; }
    public string? Process { get; set; }
    public string PNo { get; set; } = string.Empty;
}

public class ProcessTypeLink
{
    public int Id { get; set; }
    public string Process { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class MrnSpec
{
    public int Id { get; set; }
    public string Mrn { get; set; } = string.Empty;
    public string? Form { get; set; }
    public string? FullSpecification { get; set; }
    public string SpecNo { get; set; } = string.Empty;
}

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
