using DCenter.Server.Data;
using DCenter.Server.Entities;
using Microsoft.AspNetCore.DataProtection;
using DCenter.Server.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;
using System.IO;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.RateLimiting;
using DCenter.Server.Controllers;

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

if (OperatingSystem.IsWindows()) builder.Logging.AddEventLog();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "The database connection string is not set. Set the machine environment variable ConnectionStrings__DefaultConnection and restart the site.");

builder.Services.AddDbContext<WeldReportContext>(opt => opt.UseSqlServer(connectionString));

builder.Services.AddDbContext<ErpViewContext>(opt => opt.UseSqlServer(connectionString));

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync("Too many login attempts. Wait a minute and try again.", ct);
    };
    options.AddPolicy(SupervisorController.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IdempotencyGate>();

builder.Services.AddScoped<WorkOrderSearchService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ReportInsightsService>();
builder.Services.AddScoped<StoredProcedures>();
builder.Services.Configure<WeldReportOptions>(builder.Configuration.GetSection(WeldReportOptions.Section));
builder.Services.AddScoped<PdfReportService>();
builder.Services.AddScoped<ExcelReportService>();
builder.Services.Configure<ConsumableOptions>(builder.Configuration.GetSection(ConsumableOptions.Section));
builder.Services.AddScoped<ConsumableLedger>();
builder.Services.AddScoped<ConsumableItemService>();
builder.Services.AddScoped<ConsumableMovementService>();
builder.Services.AddScoped<ConsumableQueryService>();
builder.Services.AddScoped<ConsumableGuards>();
builder.Services.AddScoped<BakingService>();
builder.Services.AddScoped<OvenService>();
builder.Services.AddScoped<StockCountService>();
var keysPath = builder.Configuration["DataProtection:KeysPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "keys");
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("DCenter")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);
builder.Services.AddSingleton<SupervisorAuth>();
builder.Services.AddScoped<ISupervisorContext, HttpSupervisorContext>();
builder.Services.AddScoped<SupervisorRevocationStore>();
builder.Services.AddScoped<ConsumableImportService>();
builder.Services.AddScoped<StockImportService>();
builder.Services.AddScoped<SupervisorPasswordService>();
builder.Services.AddScoped(typeof(ReferenceTableService<>));
builder.Services.AddScoped<LookupService>();
builder.Services.AddScoped<WelderService>();
builder.Services.AddScoped<ProcessTypeLinkService>();
builder.Services.AddSingleton<IReferenceTable<BpvcMaterial>, BpvcTable>();
builder.Services.AddSingleton<IReferenceTable<MrnSpec>, MrnTable>();
builder.Services.AddSingleton<IReferenceTable<WpsItem>, WpsTable>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string DevCors = "spa-dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("https://localhost:64506", "http://localhost:64506")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Configuration.GetValue("DCenter:AutoMigrate", true))
{
    using var migrationScope = app.Services.CreateScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<WeldReportContext>();
    try
    {
        var pending = (await migrationDb.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            app.Logger.LogInformation("Applying database migrations: {Migrations}", string.Join(", ", pending));
            await migrationDb.Database.MigrateAsync();
            app.Logger.LogInformation("Database migrations applied.");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex,
            "Database migration failed, so the server did not start. Fix the error, or set DCenter__AutoMigrate=false and migrate manually.");
        throw;
    }
}

try
{
    using var scope = app.Services.CreateScope();
    var changedAt = await scope.ServiceProvider.GetRequiredService<WeldReportContext>().SupervisorCredentials.AsNoTracking()
        .Where(c => c.Id == SupervisorCredential.SingletonId)
        .Select(c => (DateTime?)c.UpdatedAt)
        .FirstOrDefaultAsync();
    var supervisorAuth = app.Services.GetRequiredService<SupervisorAuth>();
    if (changedAt is DateTime changed)
        supervisorAuth.RevokeIssuedBefore(new DateTimeOffset(DateTime.SpecifyKind(changed, DateTimeKind.Local)));
    foreach (var entry in await scope.ServiceProvider.GetRequiredService<SupervisorRevocationStore>().LoadActiveAsync(CancellationToken.None))
        supervisorAuth.Restore(entry);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not read the supervisor password date or stored logouts; existing supervisor sessions stay valid until they expire.");
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "SAMEORIGIN";
    headers["Referrer-Policy"] = "same-origin";
    await next();
});
app.UseHttpsRedirection();

// Serve the SPA from dcenter.client/dist when it sits next to the server (local release build), otherwise from wwwroot
// (published output). Placed after the error handler and security headers so static files get both.
var clientDist = Path.Combine(builder.Environment.ContentRootPath, "..", "dcenter.client", "dist");
if (Directory.Exists(clientDist))
{
    var provider = new PhysicalFileProvider(clientDist);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCors);
}

app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.Map("api/{**path}", () => Results.NotFound(
    "This API endpoint does not exist on the server. Rebuild and restart the server so it includes the latest features."));

if (Directory.Exists(clientDist))
{
    app.MapFallback(async context =>
    {
        var indexPath = Path.Combine(clientDist, "index.html");
        if (File.Exists(indexPath))
        {
            context.Response.ContentType = "text/html";
            await context.Response.SendFileAsync(indexPath);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    });
}
else
{
    app.MapFallbackToFile("/index.html");
}

app.Run();

// Lets DCenter.Server.IntegrationTests host the API with WebApplicationFactory<Program>.
public partial class Program;
