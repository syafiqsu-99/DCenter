using System.Data;
using DCenter.Server.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

// Runs dbo.SP_DCenter_* procedures on the request's WeldReportContext connection, inside its current
// transaction when one is open (so sp_getapplock ordering and transaction scope stay in C#).
public sealed class StoredProcedures(WeldReportContext db)
{
    // THROW number a procedure raises when a row it must update or delete is gone, like EF's concurrency check.
    public const int RowChanged = 50001;

    private const int MissingProcedure = 2812;

    public Task<List<T>> QueryAsync<T>(string procedure, CancellationToken ct, params SqlParameter[] parameters)
        => Run(procedure, db.Database.SqlQueryRaw<T>(Exec(procedure, parameters), Args(parameters)).ToListAsync(ct));

    public Task<List<TEntity>> EntitiesAsync<TEntity>(string procedure, CancellationToken ct, params SqlParameter[] parameters)
        where TEntity : class
        => Run(procedure, db.Set<TEntity>().FromSqlRaw(Exec(procedure, parameters), Args(parameters)).AsNoTracking().ToListAsync(ct));

    public async Task<T?> FirstOrDefaultAsync<T>(string procedure, CancellationToken ct, params SqlParameter[] parameters)
        => (await QueryAsync<T>(procedure, ct, parameters)).FirstOrDefault();

    // The single column of a procedure's single row; the column must be named Value.
    public async Task<T> ScalarAsync<T>(string procedure, CancellationToken ct, params SqlParameter[] parameters)
        => (await QueryAsync<T>(procedure, ct, parameters)).First();

    public Task<int> ExecuteAsync(string procedure, CancellationToken ct, params SqlParameter[] parameters)
        => Run(procedure, db.Database.ExecuteSqlRawAsync(Exec(procedure, parameters), Args(parameters), ct));

    // Wraps a procedure call that writes, so its SQL errors surface as the DbUpdateException that
    // SaveChanges raised before (duplicate-key and truncation checks read the inner SqlException).
    public static async Task<T> Write<T>(Task<T> call)
    {
        try
        {
            return await call;
        }
        catch (SqlException ex)
        {
            throw ex.Number == RowChanged ? new DbUpdateConcurrencyException(ex.Message, ex) : new DbUpdateException(ex.Message, ex);
        }
    }

    internal static string MissingMessage(string procedure)
        => $"Stored procedure dbo.{procedure} does not exist. Run DCenter.Server/Sql/DCenter/DCenter_StoredProcedures.sql on the DCenter database, then retry.";

    private static async Task<T> Run<T>(string procedure, Task<T> call)
    {
        try
        {
            return await call;
        }
        catch (SqlException ex) when (ex.Number == MissingProcedure)
        {
            throw new InvalidOperationException(MissingMessage(procedure), ex);
        }
    }

    internal static string Exec(string procedure, SqlParameter[] parameters)
        => parameters.Length == 0
            ? $"EXEC dbo.{procedure}"
            : $"EXEC dbo.{procedure} " + string.Join(", ", parameters.Select(p =>
                $"{p.ParameterName} = {p.ParameterName}{(p.Direction is ParameterDirection.Output or ParameterDirection.InputOutput ? " OUTPUT" : "")}"));

    private static object[] Args(SqlParameter[] parameters) => [.. parameters];
}

// Typed SqlParameter factories: every value goes in with an explicit SqlDbType.
public static class Sql
{
    public static SqlParameter Int(string name, int? value) => Make(name, SqlDbType.Int, value);
    public static SqlParameter Bit(string name, bool? value) => Make(name, SqlDbType.Bit, value);
    public static SqlParameter NVarChar(string name, string? value, int size) => Make(name, SqlDbType.NVarChar, value, size);
    public static SqlParameter NVarCharMax(string name, string? value) => Make(name, SqlDbType.NVarChar, value, -1);
    public static SqlParameter Binary(string name, byte[]? value, int size) => Make(name, SqlDbType.Binary, value, size);
    public static SqlParameter VarBinary(string name, byte[]? value, int size) => Make(name, SqlDbType.VarBinary, value, size);
    public static SqlParameter Date(string name, DateOnly? value) => Make(name, SqlDbType.Date, value?.ToDateTime(TimeOnly.MinValue));
    public static SqlParameter DateTime2(string name, DateTime? value) => Make(name, SqlDbType.DateTime2, value);
    public static SqlParameter DateTimeOffset(string name, DateTimeOffset? value) => Make(name, SqlDbType.DateTimeOffset, value);

    public static SqlParameter Decimal(string name, decimal? value, byte precision = 18, byte scale = 4)
    {
        var p = Make(name, SqlDbType.Decimal, value);
        (p.Precision, p.Scale) = (precision, scale);
        return p;
    }

    public static SqlParameter OutputInt(string name) => new(name, SqlDbType.Int) { Direction = ParameterDirection.Output };

    public static SqlParameter Table(string name, string typeName, DataTable rows)
        => new(name, SqlDbType.Structured) { TypeName = typeName, Value = rows };

    public static SqlParameter IdList(string name, IEnumerable<int> ids)
    {
        var rows = new DataTable();
        rows.Columns.Add("Id", typeof(int));
        foreach (var id in ids) rows.Rows.Add(id);
        return Table(name, "dbo.TT_DCenter_IdList", rows);
    }

    private static SqlParameter Make(string name, SqlDbType type, object? value, int size = 0)
    {
        var p = new SqlParameter(name, type) { Value = value ?? DBNull.Value };
        if (size != 0) p.Size = size;
        return p;
    }
}
