namespace DCenter.Server.Models;

public record WelderDto(int Id, string WelderName, string WelderNo, bool IsActive, string? UsageScope = "Report");
public record LookupDto(int Id, string Category, string Value, int SortOrder, bool IsActive);
public record LookupUpsert(string Category, string Value, int SortOrder, bool IsActive);
public record ProcessTypeLinkDto(int Id, string Process, string Type);
public record ProcessTypeLinkUpsert(string Process, string Type);

public class ReportDto
{
    public int Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;
    public bool ReportRequired { get; set; } = true;
    public DateOnly? DateWelded { get; set; }
    public string WorkOrderNumber { get; set; } = string.Empty;
    public string? PartNo { get; set; }
    public string? Description { get; set; }
    public string? MaterialSpec1 { get; set; }
    public string? MaterialSpec2 { get; set; }
    public string? MaterialSpec3 { get; set; }
    public string? Grade1 { get; set; }
    public string? Grade2 { get; set; }
    public string? Grade3 { get; set; }
    public string? PNumber1 { get; set; }
    public string? PNumber2 { get; set; }
    public string? PNumber3 { get; set; }
    public string? EngineerSupervisor { get; set; }
    public string? QaInspector { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RowVersion { get; set; }
    public List<JointDto> Joints { get; set; } = [];
}

public class JointDto
{
    public int Id { get; set; }
    public int JointNumber { get; set; }
    public string? PartDescLeft { get; set; }
    public string? PartNoLeft { get; set; }
    public string? HeatNumberLeft { get; set; }
    public string? PartDescRight { get; set; }
    public string? PartNoRight { get; set; }
    public string? HeatNumberRight { get; set; }
    public string? WpsNo { get; set; }
    public string? Rev { get; set; }
    public string? WelderName { get; set; }
    public string? WelderNo { get; set; }
    public List<JointMaterialDto> Materials { get; set; } = [];
}

public class JointMaterialDto
{
    public int Id { get; set; }
    public int ColumnNumber { get; set; }
    public string? Process { get; set; }
    public string? Size { get; set; }
    public string? Type { get; set; }
    public string? Manuf { get; set; }
    public string? HeatLot { get; set; }
}

public record ReportSummary(
    int Id,
    string WorkOrderNumber,
    string? PartNo,
    string? Description,
    int JointCount,
    string Status,
    DateTime UpdatedAt);

public record ReportStatusEventDto(string Action, DateTime OccurredAt, string? Details);
public record WpsDto(int Id, string WpsNo, string? BaseMetal, string? Process, string PNo);
public record WpsUpsert(string WpsNo, string? BaseMetal, string? Process, string PNo);

public record MrnSpecDto(int Id, string Mrn, string? Form, string? FullSpecification, string SpecNo, string? SpecNoRaw);
public record MrnSpecUpsert(string Mrn, string? Form, string? FullSpecification, string SpecNo, string? SpecNoRaw);

public record BpvcMaterialDto(
    int Id, string SpecNo, string? Designation, string? UnsNo, string? MinTensile, string PNo,
    string? GroupNo, string? IsoGroup, string? BrazingPNo, string? NominalComposition,
    string? TypicalProductForm, string? NominalThicknessLimits, string? SpecNoRaw);

public record BpvcMaterialUpsert(
    string SpecNo, string? Designation, string? UnsNo, string? MinTensile, string PNo,
    string? GroupNo, string? IsoGroup, string? BrazingPNo, string? NominalComposition,
    string? TypicalProductForm, string? NominalThicknessLimits, string? SpecNoRaw);