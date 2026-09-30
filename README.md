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
│  ├─ Data/                       WeldReportContext (EF model and migrations for the app tables)
│  ├─ Migrations/                 EF Core migrations: the only way schema changes
│  ├─ Sql/DCenter/                Hand-run scripts: one-time schema upgrade, work order views over OracleBetsyDB, stored procedures
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

## Architecture notes

- **Controllers stay thin.** Each controller binds input, calls one service method and maps its `ServiceResult<T>`: Consumables through `ConsumableControllerBase.ToAction`, Settings through `SettingsControllerBase`. Business rules live in `Services/`. WPS, MRN and BPVC IX share `ReferenceTableService<T>`; each table is described once in `Services/Settings/ReferenceTables.cs` (CSV columns, key, max lengths).
- **Time is injected.** Services take `TimeProvider` and use `Clock.LocalNow()` / `Clock.Today()` (local wall-clock time, same as `DateTime.Now`), so date rules are unit-testable.
- **Stock writes are serialized with `sp_getapplock`** (`Services/Consumables/StockLocks.cs`) inside the write transaction. Always acquire in this order to avoid deadlocks: `StockLocks.Master` → `StockLocks.Item(id)` (ascending id) → `StockLocks.Compartment(id)` (ascending id).
- **Supervisor sessions** are Data Protection tokens (keys DPAPI-protected on Windows). Refreshing keeps the original login time, so a session ends `SupervisorMaxSessionHours` after login. Logouts are stored in `dcenter.SupervisorRevokedTokens` and survive an app-pool recycle.
- **Kiosk endpoints are open by design.** Welders use the app without logging in, so reads (reports, welders, exports) and welder actions (counter issue/return/finish, baking, holding, saving a draft report) need no supervisor token. `X-Entered-By` is informational, not authentication.
- **Client:** server calls live in stores and composables (`useCrudApi`, `usePagedList`), never in templates. Stores: `reportStore` (editor, search, BOM tree, saved list), `reportInsightsStore` (dashboard, trace), `supervisorStore` (login session), `consumableStore` (stock screens). Shared helpers are in `src/utils` (`errors.js`, `files.js`, `timing.js`, `date.js`, `constants.js`, `fileName.js`). Code style: no semicolons, single quotes, enforced by lint.
- **SQL objects.** The database is shared with other teams, so every DCenter object lives in its own schema `dcenter` and nothing is created in `dbo`. Inside the schema, names carry no `DCenter` prefix: 20 tables (`dcenter.Welders`, `dcenter.Reports`, …), one sequence `dcenter.DocumentNoSeq`, EF's `dcenter.__EFMigrationsHistory`, two views over OracleBetsyDB (`dcenter.V_WorkOrder`, `dcenter.V_Bom`) and 57 stored procedures `dcenter.SP_<Module>_<Action>` that read and write the tables directly. `DocumentNoSeq` is one counter shared by every document number (`CT-yy-000000`, `BK-yy-0000`, `HD-yy-0000`, `SC-yy-0000`): numbers are unique and safe under concurrent saves, but one prefix's numbers are not consecutive. There are no table types: several rows or ids go into a procedure as one JSON string (`NVARCHAR(MAX)`, read with `OPENJSON`). Tables and the sequence come from EF migrations (`WeldReportContext.Schema`; a single `Baseline` migration); views and procedures live in `DCenter.Server/Sql/DCenter/` and are run by hand (see "Database scripts"). To change a procedure, edit it in `DCenter_StoredProcedures.sql` and re-run the file. C# calls procedures through `StoredProcedures` (`Services/Shared`), always with typed `SqlParameter`s (`Sql.Json` / `Sql.IdList` for JSON), and wraps every writing call in `StoredProcedures.Write` so SQL errors surface as `DbUpdateException` (a procedure `THROW 50001` for a row that is gone becomes `DbUpdateConcurrencyException`). A missing procedure is logged as "Stored procedure … does not exist. Run … DCenter_StoredProcedures.sql". Stock ledger totals are in `ConsumableLedger`, every other consumables read and write in `ConsumableStore`. EF Core is still used for migrations, transactions and `sp_getapplock`.
- **Static files** are served from `dcenter.client/dist` when it sits next to the server, otherwise from `wwwroot` (published output), after the error handler and security headers.

## Configuration

Secrets and connection strings belong in environment variables, not in `appsettings.json`:

| Variable | Purpose |
|---|---|
| `ConnectionStrings__DefaultConnection` | DCenter application database |
| `Consumables__SupervisorPassword` | Initial supervisor password (until changed in Settings) |
| `Consumables__WelderBackdateDays` | Optional: how many days back welders may date entries (default 7; supervisors are not limited) |
| `DataProtection__KeysPath` | Optional: folder for session-signing keys (default `DCenter.Server/App_Data/keys`; the IIS app pool needs write access) |
| `DCenter__AutoMigrate` | Optional: apply pending EF Core migrations at startup (default `true`; set `false` when a DBA applies scripts) |
| `WeldReport__DefaultEngineer` | Optional: Engineer / Supervisor name prefilled on the Weld Order Card PDF and Excel (default `Aizat Karim`) |
| `Consumables__SupervisorSessionHours` | Optional: lifetime of one supervisor token before it must be refreshed (default 12, 1–24) |
| `Consumables__SupervisorMaxSessionHours` | Optional: a supervisor must log in again this many hours after the original login, however often the session was refreshed (default 24) |

`appsettings.json` ships with an empty connection string. The server refuses to start, with a message naming the variable, until `ConnectionStrings__DefaultConnection` is set. Keep the keys folder outside anything a publish with "delete existing files" wipes, or supervisors are logged out on every deploy.

## Development

```bash
dotnet tool restore                                   # installs dotnet-ef
dotnet ef database update --project DCenter.Server    # apply migrations
dotnet run --project DCenter.Server                   # API + Vite dev server via SPA proxy
dotnet test DCenter.Server.Tests                      # unit tests (no database needed)
```

For local development, keep the initial supervisor password in user-secrets rather than in `launchSettings.json`:

```bash
dotnet user-secrets set "Consumables:SupervisorPassword" "<dev password>" --project DCenter.Server
```

Client checks: `npm run lint-check` reports problems without changing files; `npm run lint` applies auto-fixes.

### Database scripts

EF Core migrations own the tables and the sequence; on a new database the site creates them on first start. Views and stored procedures are plain scripts in `DCenter.Server/Sql/DCenter/`, run by hand on the DCenter database, all creating their objects in schema `dcenter`:

1. `DCenter_SchemaUpgrade.sql` (**once**, only on a database created by an earlier release, where the tables are still named `DCenter_*`): run by a **db_owner** with the site stopped. In one transaction it moves the tables (with their data) into `dcenter`, drops the `DCenter_` prefix from them and from their keys, constraints and indexes, replaces the four old number sequences with `DocumentNoSeq` (starting above every number already issued), leaves `Baseline` as DCenter's only migration history row, and drops the DCenter procedures, table types, views and functions earlier releases created. Other teams' objects are never touched. It is safe to re-run and does nothing on a new database. The server refuses to start while a `DCenter_*` table still exists.
2. `DCenter_SourceViews.sql`: `V_WorkOrder` and `V_Bom`, the views over OracleBetsyDB. Nothing is created in OracleBetsyDB. It needs:
   - the DCenter database on the same SQL Server as OracleBetsyDB, or a linked server (replace `OracleBetsyDB.dbo.` with `[server].OracleBetsyDB.dbo.` for a dev localdb);
   - SELECT on `Work_Order_Detail` and `Bill_Of_Material_Others` in OracleBetsyDB for the DefaultConnection login.
3. `DCenter_StoredProcedures.sql`: the `SP_*` procedures.

Scripts 2 and 3 are safe to re-run; re-run them whenever a release changes them. They need SQL Server 2017 or later with database compatibility level 130 or higher (`OPENJSON`; the procedures script stops with a message otherwise). With sqlcmd, pass `-I`: `sqlcmd -S <server> -d DCenter -I -b -i DCenter_StoredProcedures.sql`. If the DefaultConnection login is not `db_owner`, grant `EXECUTE ON SCHEMA::dcenter` (and DDL rights for startup auto-migrate, or let the DBA run the migration script). Until the views exist the work order search shows a message saying which step is missing; until the procedures exist the server log names the missing procedure.

Frontend only: `cd dcenter.client && npm ci && npm run dev`. API calls use relative `/api/...` paths through the Vite proxy.

## Deployment

`dotnet publish DCenter.Server -c Release`, then deploy the output to IIS. Apply pending migrations with `dotnet ef database update` (or a generated script) before switching traffic.

Run `dotnet test DCenter.Server.Tests` before every release, and work through the regression checklist in [docs/production-readiness.md](docs/production-readiness.md). On Windows servers, errors and warnings are also written to the Windows Application event log (source ".NET Runtime").
