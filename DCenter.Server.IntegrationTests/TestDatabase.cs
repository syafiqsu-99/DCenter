using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace DCenter.Server.IntegrationTests;

// A throwaway DCenter database plus a small fake OracleBetsyDB so the work order views have data.
public sealed class TestDatabase : IAsyncDisposable
{
    private readonly string server;

    public string Name { get; }
    public string ConnectionString { get; }

    private TestDatabase(string server, string name)
    {
        this.server = server;
        Name = name;
        ConnectionString = new SqlConnectionStringBuilder(server) { InitialCatalog = name }.ConnectionString;
    }

    public static async Task<TestDatabase> CreateAsync(string scenario)
    {
        var server = Environment.GetEnvironmentVariable(SqlFactAttribute.Variable)!;
        var db = new TestDatabase(server, $"DCenter_IT_{scenario}_{Guid.NewGuid():N}"[..40]);
        await db.RunAsync(server, $"CREATE DATABASE [{db.Name}]");
        await SeedErpAsync(server);
        return db;
    }

    // Applied after the API has migrated the database, as on a real server.
    public async Task ApplySourceViewsAsync()
    {
        var script = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", "DCenter_SourceViews.sql"));
        foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            if (!string.IsNullOrWhiteSpace(batch)) await RunAsync(ConnectionString, batch);
    }

    private static async Task SeedErpAsync(string server)
    {
        const string sql = """
            IF DB_ID('OracleBetsyDB') IS NULL CREATE DATABASE OracleBetsyDB;
            """;
        await RunStatic(server, sql);
        var erp = new SqlConnectionStringBuilder(server) { InitialCatalog = "OracleBetsyDB" }.ConnectionString;
        await RunStatic(erp, """
            IF OBJECT_ID('dbo.Work_Order_Detail') IS NULL
            BEGIN
                CREATE TABLE dbo.Work_Order_Detail (WO_NUMBER nvarchar(100), ASSEMBLY_ITEM nvarchar(100), ITEM_DESC nvarchar(400), START_QUANTITY decimal(18,4));
                CREATE TABLE dbo.Bill_Of_Material_Others (ITEM nvarchar(100), COMPONENT nvarchar(100), COMPONENT_DESC nvarchar(400));
                INSERT dbo.Work_Order_Detail VALUES
                    ('WO100', 'ASM-100', 'Valve body assembly', 2), ('WO101', 'ASM-101', 'Bonnet assembly', 1),
                    ('WO102', 'ASM-100', 'Valve body assembly', 4), ('WO200', 'ASM-200', 'Actuator yoke', 1);
                INSERT dbo.Bill_Of_Material_Others VALUES
                    ('ASM-100', 'BODY-1', 'Body casting'), ('ASM-100', 'FLG-2', 'Flange'), ('BODY-1', 'PLATE-9', 'Plate'),
                    ('ASM-101', 'BON-1', 'Bonnet'), ('ASM-100', 'FLG-2', 'Flange (dup)');
            END
            """);
    }

    private Task RunAsync(string connectionString, string sql) => RunStatic(connectionString, sql);

    private static async Task RunStatic(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DisposeAsync()
    {
        SqlConnection.ClearAllPools();
        await RunAsync(server, $"ALTER DATABASE [{Name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{Name}];");
    }
}
