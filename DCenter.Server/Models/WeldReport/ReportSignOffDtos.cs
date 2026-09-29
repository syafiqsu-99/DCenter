namespace DCenter.Server.Models;

public record WelderSignOff(string? WelderName, string? WelderNo, string? DisplayName, DateOnly? Date);

public record ReportSignOff(
    string? EngineerName, DateOnly? EngineerDate, string? QaName, DateOnly? QaDate, List<WelderSignOff>? Welders);
