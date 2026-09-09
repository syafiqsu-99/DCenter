using DCenter.Server.Data;
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

// Writable new DB (connection string from ConnectionStrings__DefaultConnection env var).
builder.Services.AddDbContext<WeldReportContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Read-only source DB (ConnectionStrings__SourceConnection env var).
builder.Services.AddDbContext<SourceContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SourceConnection")));

builder.Services.AddScoped<JobSearchService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<PdfReportService>();
builder.Services.AddScoped<ExcelReportService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

const string DevCors = "spa-dev";
builder.Services.AddCors(o => o.AddPolicy(DevCors, p => p
    .WithOrigins("https://localhost:64506", "http://localhost:64506")
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Serve client static files from the SPA "dist" folder when present.
// During development the SPA dev server (Vite) will still be used if started separately
// or via the SPA proxy; this ensures the server can serve built assets in other cases.
var clientDist = Path.Combine(builder.Environment.ContentRootPath, "..", "dcenter.client", "dist");
if (Directory.Exists(clientDist))
{
    var provider = new PhysicalFileProvider(clientDist);
    // Serve default files (index.html) and static assets from the client dist folder
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = provider });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = provider });
}
else
{
    // Fallback to default behavior (use default files from web root)
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
// If the client dist exists, map a fallback that serves its index.html. Otherwise use the
// existing MapFallbackToFile which will serve the server's index.html (if present).
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

// Ensure the writable DB exists and apply any migrations on startup.
// If there are no migrations in the assembly, fall back to EnsureCreated so
// a deleted database is recreated from the EF model.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        var db = services.GetRequiredService<WeldReportContext>();

        // Prefer migrations when available
        var pending = db.Database.GetPendingMigrations();
        if (pending != null && pending.Any())
        {
            logger.LogInformation("Applying {Count} pending EF Core migrations.", pending.Count());
            db.Database.Migrate();
        }
        else
        {
            logger.LogInformation("No migrations found; ensuring database is created.");
            db.Database.EnsureCreated();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or initializing the database.");
        // Rethrow to avoid running the app in a broken state
        throw;
    }
}

app.Run();
