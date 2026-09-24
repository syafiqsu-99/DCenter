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
