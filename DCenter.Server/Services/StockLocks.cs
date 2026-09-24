using System.Data;
using DCenter.Server.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DCenter.Server.Services;

internal static class StockLocks
{
    public const string Master = "DCenter.Consumables.Master";

    public static string Item(int itemId) => $"DCenter.Consumables.Item.{itemId}";

    public static string Compartment(int compartmentId) => $"DCenter.Consumables.Compartment.{compartmentId}";

    public static async Task AcquireAsync(WeldReportContext db, string resource, CancellationToken ct)
    {
        var result = new SqlParameter("@result", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var name = new SqlParameter("@resource", SqlDbType.NVarChar, 255) { Value = resource };
        await db.Database.ExecuteSqlRawAsync(
            "EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000",
            new object[] { result, name }, ct);
        if (result.Value is not int code || code < 0)
            throw new TimeoutException("Another user is updating this stock right now. Please try again.");
    }
}
