using DCenter.Server.Entities;
using DCenter.Server.Models;

namespace DCenter.Server.Services;

// The sign-off block printed on every joint of the Weld Order Card, as display text.
internal sealed record ResolvedSignOff(
    string EngineerName, string EngineerDate, string QaName, string QaDate,
    IReadOnlyDictionary<string, (string Name, string Date)> Welders)
{
    public (string Name, string Date)? WelderFor(Joint j)
        => Welders.TryGetValue(ReportSignOffRules.WelderKey(j.WelderName, j.WelderNo), out var w) ? w : null;
}

// Sign-offs are chosen at export time and are not stored on the report.
public static class ReportSignOffRules
{
    public const int MaxNameLength = 100;

    internal static string WelderKey(string? name, string? number)
        => $"{name?.Trim().ToUpperInvariant()}\u001f{number?.Trim().ToUpperInvariant()}";

    public static string WelderDisplay(string? name, string? number)
        => string.IsNullOrWhiteSpace(name) ? "" : $"{name} (ID {number})";

    public static ReportSignOff Defaults(Report r, DateOnly today, string engineer)
    {
        var welders = r.Joints.OrderBy(j => j.JointNumber)
            .Where(j => !string.IsNullOrWhiteSpace(j.WelderName))
            .DistinctBy(j => WelderKey(j.WelderName, j.WelderNo))
            .Select(j => new WelderSignOff(j.WelderName, j.WelderNo, WelderDisplay(j.WelderName, j.WelderNo), r.DateWelded))
            .ToList();
        return new ReportSignOff(engineer, today, "", null, welders);
    }

    // No input prints the defaults; with input, the given values are printed as entered (a cleared field prints
    // blank), and welders missing from the input keep their defaults.
    internal static ResolvedSignOff Resolve(Report r, ReportSignOff? input, DateOnly today, string engineer)
    {
        var defaults = Defaults(r, today, engineer);
        var chosen = input ?? defaults;

        var welders = new Dictionary<string, (string, string)>();
        foreach (var d in defaults.Welders!)
        {
            var key = WelderKey(d.WelderName, d.WelderNo);
            var entered = input?.Welders?.FirstOrDefault(w => WelderKey(w.WelderName, w.WelderNo) == key);
            welders[key] = entered is null
                ? (d.DisplayName ?? "", WeldCardLayout.FormatDate(d.Date))
                : (Name(entered.DisplayName), WeldCardLayout.FormatDate(entered.Date));
        }

        return new ResolvedSignOff(
            Name(chosen.EngineerName), WeldCardLayout.FormatDate(chosen.EngineerDate),
            Name(chosen.QaName), WeldCardLayout.FormatDate(chosen.QaDate),
            welders);
    }

    private static string Name(string? value)
    {
        var name = value?.Trim() ?? "";
        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }
}
