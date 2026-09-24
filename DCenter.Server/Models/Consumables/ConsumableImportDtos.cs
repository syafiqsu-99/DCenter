namespace DCenter.Server.Models;

public record ImportRowDto(int Line, string Action, string? Category, string? DiaSpec, List<string> Messages);

public record ImportResultDto(
    bool Committed, int TotalRows, int Created, int Updated, int Unchanged, int Rejected, List<ImportRowDto> Rows,
    List<string> FileErrors);
