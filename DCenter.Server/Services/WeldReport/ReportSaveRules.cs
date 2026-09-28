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

    public static bool IsDuplicateKey(Exception ex)
        => ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
           && sql.Number is UniqueIndexViolation or UniqueConstraintViolation;
}
