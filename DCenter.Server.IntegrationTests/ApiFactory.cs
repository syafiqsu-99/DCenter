using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DCenter.Server.IntegrationTests;

// The real API against a test database, with a fixed clock so dates in responses are repeatable.
public sealed class ApiFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    public const string SupervisorPassword = "Scenario-Password-1";

    public static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("UTC+8", TimeSpan.FromHours(8), "UTC+8", "UTC+8");

    public FixedTime Time { get; } = new(new DateTimeOffset(2026, 9, 29, 2, 0, 0, TimeSpan.Zero), Zone);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:DefaultConnection", database.ConnectionString);
        builder.UseSetting("DCenter:AutoMigrate", "true");
        builder.UseSetting("Consumables:SupervisorPassword", SupervisorPassword);
        builder.UseSetting("DataProtection:KeysPath", Path.Combine(Path.GetTempPath(), "dcenter-it-keys", database.Name));
        builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Time)));
    }
}

public sealed class FixedTime(DateTimeOffset utcNow, TimeZoneInfo zone) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => Now;
    public override TimeZoneInfo LocalTimeZone => zone;
}
