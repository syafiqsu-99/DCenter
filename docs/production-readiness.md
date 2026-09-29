# Production readiness checklist

Work through this list for every release. Tick each box in the release ticket, not in this file.

## 1. Build and test

- [ ] `dotnet test DCenter.Server.Tests` passes.
- [ ] `cd dcenter.client && npm ci && npm run lint-check` passes (no `--fix`, no file changes).
- [ ] `dotnet publish DCenter.Server -c Release` succeeds.

## 2. Database

- [ ] Review pending migrations: `dotnet ef migrations script --idempotent --project DCenter.Server -o release.sql`.
- [ ] Back up the DCenter database.
- [ ] Apply migrations (startup auto-migrate with `DCenter__AutoMigrate` unset/true, or run `release.sql` as the DBA).
- [ ] If `Sql/DCenter/DCenter_SourceViews.sql` changed, re-run it (safe to re-run).
- [ ] `dotnet ef migrations has-pending-model-changes --project DCenter.Server --context WeldReportContext` reports no changes.

## 3. Server configuration (IIS host)

- [ ] `ConnectionStrings__DefaultConnection` is set as a machine-level environment variable.
- [ ] `Consumables__SupervisorPassword` is set if no password has been saved in Settings yet.
- [ ] `DataProtection__KeysPath` points outside the publish folder, and the app pool identity can write to it.
- [ ] App pool: .NET CLR "No Managed Code", Start Mode `AlwaysRunning`, Idle Time-out `0`; site Preload enabled.
- [ ] HTTPS binding and certificate valid.

## 4. Smoke test after deploy

Weld Report
- [ ] Search a work order, open the BOM tree, start a report.
- [ ] Pick Material + Grade: P-No fills from BPVC and the WPS list narrows.
- [ ] Save, complete, reopen (supervisor), download PDF and Excel.
- [ ] Dashboard and Trace load.

Consumables
- [ ] Supervisor login, refresh after an hour, logout. After logout, recycle the app pool and confirm the old session does not come back.
- [ ] Open the site from the published output: the page loads its scripts (no blank page, no 404s for `/assets/*` in the browser console).
- [ ] Receive → Transfer to Activated → Send to bake → Start/Stop → Place in oven.
- [ ] Counter: issue, return, finish for a welder.
- [ ] Adjust Activated stock, then void that transaction.
- [ ] Stock count: save a count and void it.
- [ ] Consumable master CSV and stock CSV import: preview, then commit.

Settings
- [ ] Welders, dropdown lists, process → type links: add, edit, delete.
- [ ] WPS, MRN, BPVC IX: export CSV, change one non-key value, import → reported as "1 updated"; re-import the same file → "unchanged".
- [ ] Dropdown lists: export, re-import → all "unchanged"; a value starting with `=` exports as `'=`.
- [ ] Entering a value longer than the column allows shows a plain 400 message, not a server error.
- [ ] Change the supervisor password; old sessions are signed out.

## 5. After release

- [ ] Windows Application event log shows no new errors from the app.
- [ ] Record the deployed commit hash in the release ticket.
