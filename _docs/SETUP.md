# Weld Report — setup

Drop the `DCenter.Server` and `dcenter.client` files into your existing repo
(they mirror the repo layout). Then:

## 1. Server packages

From `DCenter.Server/` — the versions are already in the updated `.csproj`, so a
restore is enough:

    dotnet restore

If you prefer to add them explicitly instead of using the edited csproj:

    dotnet add package Microsoft.EntityFrameworkCore.SqlServer
    dotnet add package Microsoft.EntityFrameworkCore.Design
    dotnet add package Microsoft.Data.SqlClient
    dotnet add package Swashbuckle.AspNetCore
    dotnet add package QuestPDF
    dotnet add package ClosedXML

## 2. EF Core CLI (local tool, per baseline)

    dotnet new tool-manifest        # if .config/dotnet-tools.json does not exist yet
    dotnet tool install dotnet-ef   # keep pinned version aligned with EF runtime packages
    dotnet tool restore

## 3. Connection strings (system environment variables — never appsettings.json)

    ConnectionStrings__DefaultConnection   -> the NEW weld-report DB (created by migration)
    ConnectionStrings__SourceConnection    -> the EXISTING read-only work-order DB

Double-underscore convention; set these as machine-level system env vars.

## 4. Create the new database

Only `WeldReportContext` is migrated. `SourceContext` is read-only and must be
excluded from every EF command with `--context`:

    dotnet ef migrations add InitialCreate --context WeldReportContext
    dotnet ef database update --context WeldReportContext

## 5. Logo assets (for PDF/Excel)

Put the two PNGs here:

    DCenter.Server/Assets/emerson.png
    DCenter.Server/Assets/fisher.png

They are copied to output by the csproj `<None Include="Assets\**" />` item.
If a file is missing, generation still works — the logo is simply omitted.

## 6. Client packages

From `dcenter.client/`:

    npm install

## 7. Run

Run the server profile (the SPA proxy launches `npm run dev` on port 64506).
Swagger is at `/swagger` in Development.

## Notes

- Two DbContexts share one SQL Server instance or two — driven purely by the two
  connection strings. Only DefaultConnection is written to.
- The source query is in `Services/JobSearchService.cs`, parameterized via
  `SqlParameter`. Adjust the schema/table names there if they differ.
- PDF = Image 1 (WELD SHOP JOB REPORT), inline display. Excel = Image 2
  (F-WD-005 Weld Order Card), download. Both driven from the same saved draft.
