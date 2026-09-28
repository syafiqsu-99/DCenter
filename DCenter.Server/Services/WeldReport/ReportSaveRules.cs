using DCenter.Server.Entities;
using DCenter.Server.Models;

namespace DCenter.Server.Services;

public static class ReportSaveRules
{
    public const int UniqueIndexViolation = 2601;
    public const int UniqueConstraintViolation = 2627;

    // Null when the save may proceed; otherwise the reason it must be refused.
    public static string? Conflict(Report? existing, ReportDto dto)
    {
        if (existing is null) return null;
        if (existing.CompletedAt is not null)
            return "This report is completed. Ask a supervisor to reopen it before making changes.";
        if (dto.Id != existing.Id || string.IsNullOrEmpty(dto.RowVersion))
            return $"A report for work order {existing.WorkOrderNumber} already exists. Open it from Saved reports instead of starting a new one.";
        if (!Convert.TryFromBase64String(dto.RowVersion, new byte[dto.RowVersion.Length], out _))
            return "This report's version stamp is not valid. Reload the report and try again.";
        return null;
    }

    public const int MaxJoints = 50;

    // What is still missing before the report may be marked complete; empty when it is ready.
    public static List<string> CompletionProblems(Report report)
    {
        if (report.Joints.Count == 0) return ["Add at least one joint."];

        static bool Blank(string? v) => string.IsNullOrWhiteSpace(v);
        var problems = new List<string>();
        foreach (var joint in report.Joints.OrderBy(j => j.JointNumber))
        {
            var missing = new List<string>();
            if (Blank(joint.WpsNo)) missing.Add("WPS No.");
            if (Blank(joint.WelderName)) missing.Add("Welder Name");
            if (Blank(joint.WelderNo)) missing.Add("Welder No.");
            if (Blank(joint.HeatNumberLeft)) missing.Add("Heat Number (joining of)");
            if (Blank(joint.HeatNumberRight)) missing.Add("Heat Number (with)");
            var first = joint.Materials.FirstOrDefault(m => m.ColumnNumber == 1);
            if (Blank(first?.Process)) missing.Add("Electrode 1 Process");
            if (Blank(first?.Type)) missing.Add("Electrode 1 Type");
            if (Blank(first?.HeatLot)) missing.Add("Electrode 1 Heat/Lot");
            if (missing.Count > 0) problems.Add($"Joint {joint.JointNumber}: {string.Join(", ", missing)}");
        }
        return problems;
    }

    public static string CompletionMessage(IReadOnlyList<string> problems, int show = 10)
    {
        var shown = string.Join("; ", problems.Take(show));
        var more = problems.Count > show ? $"; and {problems.Count - show} more joint(s)" : "";
        return $"Complete these before marking the report complete: {shown}{more}.";
    }

    // Stored joint numbers are always 1..n in the order the user arranged them.
    public static List<JointDto> Renumber(IEnumerable<JointDto> joints)
        => joints
            .Select((j, index) => (Joint: j, Index: index))
            .OrderBy(x => x.Joint.JointNumber)
            .ThenBy(x => x.Index)
            .Select((x, i) => { x.Joint.JointNumber = i + 1; return x.Joint; })
            .ToList();

    public static bool IsDuplicateKey(Exception ex)
        => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
           && sql.Number is UniqueIndexViolation or UniqueConstraintViolation;
}
