namespace DCenter.Server.IntegrationTests;

// Runs only when DCENTER_TEST_SQL points at a SQL Server instance (server level, no database name).
public sealed class SqlFactAttribute : FactAttribute
{
    public const string Variable = "DCENTER_TEST_SQL";

    public SqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable)))
            Skip = $"Set {Variable} to a SQL Server connection string (for example Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True) to run the integration scenarios.";
    }
}
