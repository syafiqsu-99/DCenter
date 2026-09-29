using System.Text.Json.Nodes;

namespace DCenter.Server.IntegrationTests;

// One scenario = one fresh database, one API host and one golden snapshot.
public abstract class Scenario : IAsyncLifetime
{
    protected TestDatabase Database { get; private set; } = null!;
    protected ApiFactory Api { get; private set; } = null!;
    protected Snapshot Snap { get; private set; } = null!;

    protected abstract string Name { get; }

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlFactAttribute.Variable))) return;
        Database = await TestDatabase.CreateAsync(Name);
        Api = new ApiFactory(Database);
        var client = Api.CreateClient();
        await Database.ApplySourceViewsAsync();
        Snap = new Snapshot(client, Name, Api.Time);
    }

    public async Task DisposeAsync()
    {
        if (Api is null) return;
        await Api.DisposeAsync();
        await Database.DisposeAsync();
    }

    protected async Task LoginSupervisorAsync(string name = "QA Lead")
    {
        var session = await Snap.Post("supervisor login", "/api/supervisor/login",
            new { name, password = ApiFactory.SupervisorPassword });
        Snap.Client.DefaultRequestHeaders.Remove("X-Supervisor-Token");
        Snap.Client.DefaultRequestHeaders.Add("X-Supervisor-Token", session!["token"]!.GetValue<string>());
    }

    protected void EnteredBy(string? name)
    {
        Snap.Client.DefaultRequestHeaders.Remove("X-Entered-By");
        if (name is not null) Snap.Client.DefaultRequestHeaders.Add("X-Entered-By", Uri.EscapeDataString(name));
    }

    protected static int Id(JsonNode? node, string property = "id") => node![property]!.GetValue<int>();
}
