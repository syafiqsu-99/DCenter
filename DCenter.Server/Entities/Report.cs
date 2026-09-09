namespace DCenter.Server.Entities;

// One report per job number (editable draft). JobNumber is unique.
public class Report
{
    public int Id { get; set; }
    public string JobNumber { get; set; } = string.Empty;

    // Header block (all free-text per requirements).
    public bool ReportRequired { get; set; } = true;
    public DateTime? DateWelded { get; set; }
    public string? WorkOrder { get; set; }
    public string? PartNo { get; set; }
    public string? Description { get; set; }

    // Material spec columns 1..3 (hard max 3).
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

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Joint> Joints { get; set; } = [];
}

public class Joint
{
    public int Id { get; set; }
    public int ReportId { get; set; }
    public Report Report { get; set; } = null!;

    public int JointNumber { get; set; }

    // "Joining of" side
    public string? PartDescLeft { get; set; }
    public string? PartNoLeft { get; set; }
    public string? HeatNumberLeft { get; set; }
    // "with" side
    public string? PartDescRight { get; set; }
    public string? PartNoRight { get; set; }
    public string? HeatNumberRight { get; set; }

    public string? WpsNo { get; set; }
    public string? Rev { get; set; }
    public string? WelderName { get; set; }
    public string? WelderNo { get; set; }

    public List<JointMaterial> Materials { get; set; } = [];
}

// Electrode Data column (1..3) for a joint.
public class JointMaterial
{
    public int Id { get; set; }
    public int JointId { get; set; }
    public Joint Joint { get; set; } = null!;

    public int ColumnNumber { get; set; } // 1..3
    public string? Process { get; set; }
    public string? Size { get; set; }
    public string? Type { get; set; }
    public string? Manuf { get; set; }
    public string? HeatLot { get; set; }
}
