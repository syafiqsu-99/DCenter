# DCenter Operations Hub

Internal web app for the Emerson / Fisher weld shop. It has three modules:

| Module | What it does | UI route |
|---|---|---|
| **Weld Report** | Weld Shop Job Report (form F-WD-005) per work order, exported as PDF or Excel | `/` |
| **Consumables** | Welding filler-metal inventory: receiving, Normal → Activated storage, electrode baking, holding ovens, welder use/return, stock audit | `/consumables` |
| **Settings** | Master data: welders, dropdown lists, WPS, MRN, BPVC IX, supervisor password | `/settings` (supervisor only) |

Welders use the app without logging in. Consumables opens on **Welder View**. A supervisor password unlocks the other Consumables tabs and Settings.

### Settings CSV import/export

The WPS, MRN and BPVC IX tables export to CSV and import it back. Import finds columns by header name, so column order doesn't matter. Each row is matched on its key columns (case and extra spaces ignored). If the key matches an existing row, the other columns update that row. If the key is new, the row is added. Rows missing a required key value are skipped. Add/Edit in the UI rejects a key that already exists.

| Table | Key columns | Updated on import |
|---|---|---|
| WPS | `WpsNo`, `PNo` | `BaseMetal`, `Process` |
| MRN | `MRN`, `SpecNo` | `Form`, `FullSpecification` |
| BPVC IX | `SpecNo`, `Designation` (grade), `UnsNo`, `PNo` | all other columns |

The Weld Report reads only BPVC `SpecNo`, `Designation`/`UnsNo` and `PNo`, to fill P-No. and narrow the WPS list. Older exports with a `SpecNoRaw` column still import: `SpecNoRaw` is read as `SpecNo`, and the old normalised `SpecNo` column is used only when `SpecNoRaw` is blank.

## Solution layout

```
DCenter/
├─ .config/dotnet-tools.json      dotnet-ef, pinned to the EF Core package version
├─ DCenter.Server.Tests/          xUnit tests for ledger, guards, CSV, auth, idempotency and report rules
├─ DCenter.Server/                ASP.NET Core Web API (.NET 10, EF Core, SQL Server)
│  ├─ Program.cs                  DI, data protection, SPA hosting
│  ├─ Data/                       WeldReportContext (app DB), ErpViewContext (read-only work order views), EF configuration
│  ├─ Migrations/                 EF Core migrations: the only way schema changes
│  ├─ Sql/DCenter/                Work order views over OracleBetsyDB, run once by hand
│  ├─ Assets/                     Logos embedded in the PDF / Excel report
│  ├─ Controllers/  Services/  Entities/  Models/
│  │    each split into:  Consumables/  WeldReport/  Settings/  Supervisor/  (+ Services/Shared)
│  └─ appsettings.json            keep secrets out; see "Configuration"
└─ dcenter.client/                Vue 3 + Vite + Vuetify + Pinia + Chart.js
   └─ src/
      ├─ router/  store/  utils/  composables/  plugins/  assets/
      ├─ views/                   report/  consumables/  settings/     (page skeletons only)
      └─ components/
         ├─ common/               app-wide pieces (sticky bar, confirm dialog, supervisor login)
         ├─ report/               weld report screens
         ├─ settings/             master-data tables
         └─ consumables/          one folder per tab: dashboard, receiving, inventory, transfers, baking,
                                  holding, items, stock-audit, history, welder, print, plus shared/
```

C# namespaces stay flat (`DCenter.Server.Services`, `.Entities`, `.Models`, `.Controllers`). The module folders exist only to make files easier to find.

## Configuration

Secrets and connection strings belong in environment variables, not in `appsettings.json`:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | DCenter application database |
| `Consumables__SupervisorPassword` | Initial supervisor password (until changed in Settings) |
| `Consumables__WelderBackdateDays` | Optional: how many days back welders may date entries (default 7; supervisors are not limited) |
| `DataProtection__KeysPath` | Optional: folder for session-signing keys (default `DCenter.Server/App_Data/keys`; the IIS app pool needs write access) |

`appsettings.json` ships with an empty connection string. The server refuses to start, with a message naming the variable, until `ConnectionStrings__DefaultConnection` is set. Keep the keys folder outside anything a publish with "delete existing files" wipes, or supervisors are logged out on every deploy.

## Development

```bash
dotnet tool restore                                   # installs dotnet-ef
dotnet ef database update --project DCenter.Server    # apply migrations
dotnet run --project DCenter.Server                   # API + Vite dev server via SPA proxy
dotnet test DCenter.Server.Tests                      # unit tests (no database needed)
```

### Work order views

Work orders and BOM levels come from OracleBetsyDB through views in the DCenter database, so nothing is created in OracleBetsyDB. Run `DCenter.Server/Sql/DCenter/DCenter_SourceViews.sql` once on the DCenter database (it is safe to re-run). It needs:

- the DCenter database on the same SQL Server as OracleBetsyDB, or a linked server (replace `OracleBetsyDB.dbo.` with `[server].OracleBetsyDB.dbo.` for a dev localdb);
- SELECT on `Work_Order_Detail` and `Bill_Of_Material_Others` in OracleBetsyDB for the DefaultConnection login.

Until then the work order search shows a message saying which step is missing.

Frontend only: `cd dcenter.client && npm ci && npm run dev`. API calls use relative `/api/...` paths through the Vite proxy.

## Deployment

`dotnet publish DCenter.Server -c Release`, then deploy the output to IIS. Apply pending migrations with `dotnet ef database update` (or a generated script) before switching traffic.

Run `dotnet test DCenter.Server.Tests` before every release, and work through the regression checklist in [docs/production-readiness.md](docs/production-readiness.md). On Windows servers, errors and warnings are also written to the Windows Application event log (source ".NET Runtime").
