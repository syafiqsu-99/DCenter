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

QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<WeldReportContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<SourceContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SourceConnection")));

builder.Services.AddScoped<WorkOrderSearchService>();
builder.Services.AddScoped<ReportService>();
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
builder.Services.AddDataProtection()
    .SetApplicationName("DCenter")
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddSingleton<SupervisorAuth>();
builder.Services.AddScoped<ConsumableImportService>();
builder.Services.AddScoped<SupervisorPasswordService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string DevCors = "spa-dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("https://localhost:64506", "http://localhost:64506")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var changedAt = await scope.ServiceProvider.GetRequiredService<WeldReportContext>().SupervisorCredentials.AsNoTracking()
        .Where(c => c.Id == SupervisorCredential.SingletonId)
        .Select(c => (DateTime?)c.UpdatedAt)
        .FirstOrDefaultAsync();
    if (changedAt is DateTime changed)
        app.Services.GetRequiredService<SupervisorAuth>()
            .RevokeIssuedBefore(new DateTimeOffset(DateTime.SpecifyKind(changed, DateTimeKind.Local)));
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not read the supervisor password date; existing supervisor sessions stay valid until they expire.");
}

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
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevCors);
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

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

//var autoMigrate = app.Environment.IsDevelopment()
//    || app.Configuration.GetValue<bool>("DCenter_AutoMigrate");

//if (autoMigrate)
//{
//    using var scope = app.Services.CreateScope();
//    var services = scope.ServiceProvider;
//    var logger = services.GetRequiredService<ILogger<Program>>();
//    try
//    {
//        var db = services.GetRequiredService<WeldReportContext>();
//        db.Database.Migrate();
//        logger.LogInformation("Database migrations applied (or already up to date).");
//    }
//    catch (Exception ex)
//    {
//        logger.LogError(ex, "Failed to apply database migrations on startup.");
//        throw;
//    }
//}

app.Run();