# DCenter code quality review

Scope: `DCenter.Server`, `DCenter.Server.Tests`, `dcenter.client/src` (migrations excluded).

The rule for every change in this review is simple: it must preserve behavior. Nothing may change business rules, workflows, outputs, API contracts or database behavior. Findings that would change observable behavior, even for the better, are listed as recommendations and were not applied.

**Status legend:** ✅ done in this pass · ⏳ recommended, behavior-preserving, deferred until it can be built and tested between steps · ⚠️ recommended, changes observable behavior, needs sign-off.

---

## 1. Executive summary

DCenter is in good shape for a small internal IIS app. Its strengths:
- Module layering is clear.
- Supervisor auth is careful: PBKDF2 with 210k iterations, constant-time compare, a rate-limited login, and time-limited Data Protection tokens.
- Every raw SQL statement is parameterized.
- Stock writes are serialized with `sp_getapplock` in a consistent lock order.
- The core ledger rules have 66 DB-free unit tests.

Debt is concentrated in five places:

1. **Two architectures in one app.** Consumables uses thin controllers, services and `ServiceResult<T>`. The Settings controllers query `WeldReportContext` directly and hold validation and import logic.
2. **Large service classes.** `ConsumableMovementService` is 812 lines with methods up to 144 lines. `StockImportService.PlanRow` is 177 lines.
3. **Duplicated CSV infrastructure.** There are 3 header mappers, 2 CSV writers and several client download paths.
4. **Broad client stores.** `consumableStore` (637 lines) mixes supervisor auth with 12 stock concerns. `reportStore` (707 lines) mixes the editor, search, BOM tree, dashboard and trace.
5. **Tooling was red.** `npm run lint` reported 258 errors. All but 19 came from misconfiguration, and the command rewrote files through `--fix`. ✅ Fixed.

## 2. Overall assessment

| Area | Grade | Notes |
|---|---|---|
| Architecture | B | Clear module folders. The Settings module breaks the controller/service pattern. |
| Readability | B− | Long methods in the Consumables services; status and stage literals. |
| Maintainability | B− | CSV and import duplication; god stores on the client. |
| Performance | B+ | No hot spots. A few N+1 queries inside stock locks. |
| Security | B | Solid auth core. Formula-injection gaps on two exports. Unencrypted Data Protection keys. A committed dev password ✅ removed. |
| Error handling | B− | Unmapped `DbUpdateException` → 500 in Settings. Some silent client catches. |
| Testability | B | Ledger and guards are tested. Services read `DateTime.Now` and `HttpContext` directly. |
| Consistency | C+ | Three API-result conventions; mixed semicolon style on the client. |
| Documentation | B | Good README. ✅ Added the missing release checklist and architecture notes. |

## 3. Architecture review

- **Layering.**
  - Consumables: `ConsumableControllerBase` → service → `ServiceResult<T>` → `ToAction`. This is the reference pattern.
  - Weld Report: `ReportsController` → `ReportService` with enum results plus `ReportConflictException`, a third convention.
  - Settings: `Bpvc/Mrn/Wps/Lookups/Welders/ProcessTypeLinks` controllers validate, deduplicate, import and save directly. ⏳ Move them to `Services/Settings/*Service` returning `ServiceResult`, with the same routes, status codes and messages.
- **Coupling.**
  - `WeldersController.Delete` queries Consumables tables (`WeldersController.cs:87`).
  - `ConsumableGuards` reads `IHttpContextAccessor` and `SupervisorAuth` to decide backdating (`ConsumableGuards.cs:22-40`), so a domain service depends on HTTP. ⏳ Pass `bool isSupervisor` from the controller, which already computes it.
  - `ConsumableControllerBase` resolves services through `RequestServices` and decrypts the supervisor token up to 3× per request. ⏳ Cache the result in `HttpContext.Items`.
- **God classes.**
  - Server: `ConsumableMovementService` (8 commands), `ConsumableQueryService` (dashboard + stock + counter), `ReportService` (CRUD + dashboard + trace).
  - Client: `consumableStore`, `reportStore`.
- **DI.**
  - Lifetimes are correct: the singletons capture only singletons, and there are no hosted services.
  - `TimeProvider` isn't registered, so `IdempotencyGate` silently uses its parameterless constructor.
  - `AddSwaggerGen` is registered in every environment.
- **Pipeline.** `UseStaticFiles` runs before `UseExceptionHandler` and the security-headers middleware, so static responses lack those headers. ⚠️ Moving it adds headers.
- **No circular dependencies were found.**

## 4. Maintainability findings

- **CSV stack duplication.**
  - Header mappers: `ConsumableImportService.cs:254`, `StockImportService.cs:394`, `ReferenceCsv.cs:33`.
  - The upload prelude is repeated 3×.
  - `Key(spec, dia)` and kg parsing appear twice each.
  - `CsvText.ToCsv` (unguarded) vs `CsvText.Write`.
  - ✅ The 2 MB and request-limit constants are unified: `CsvText.RequestLimitBytes`.
  - ⏳ Shared `CsvHeaderMap`, with each normalizer passed as a parameter.
- **Settings controllers.** Bpvc/Mrn/Wps share one shape. ✅ The duplicated import-conflict message is shared. ✅ `LookupsController` reuses `ReportSaveRules.IsDuplicateKey`.
- **Movement service repetition.**
  - The validation preamble appears 8×.
  - The FIFO take block appears 2× and differs in `OrderBy(LotId)`, so a shared helper needs an order flag.
  - The movement-row builder appears 7×.
  - ⏳ Extract private helpers; keep lock order and transactions identical.
- **Client duplication.**
  - ✅ `useCrudApi` for all settings tables.
  - ✅ `usePagedList` for the four paged history tables.
  - ✅ A single report file-name builder.
  - ✅ General helpers moved out of `utils/consumables.js`.
  - ⏳ Share the CRUD table shell (search, select, delete-confirm, dialog) across `ReferenceTable`/`LookupTable`/`WelderTable`, and add a `useDebouncedSearch` composable for the 4 pickers.

## 5. Readability findings

- **Long methods.**
  - `ConsumableMovementService`: `VoidAsync` 572-715, `AdjustAsync` 461-570, `ReturnAsync` 215-314.
  - `StockImportService`: `PlanRow` 212-388, `ImportAsync` 74-210.
  - `ConsumableImportService.PlanRow`.
  - `ReportService`: `GetDashboardAsync`, `SaveAsync`.
  - ⏳ Split into named steps.
- **Magic values, server.**
  - ✅ Report status and action strings are now `ReportStatus` and `ReportAction`.
  - ✅ Page-size clamps now use `ConsumableText.MaxPageSize`.
  - ✅ The Lookups category list now uses the `ConsumableItemService` constants.
  - ✅ `WarningPrefix` is reused.
  - ⏳ Field lengths `500`/`100`/`60`, `Take(30/4/200/20)`, the lock timeout `10000`, and `Math.Max(ReturnWindowDays, 1)` ×3.
- **Magic values, client.**
  - ✅ `utils/constants.js` holds the supervisor header, report statuses and actions, and `MAX_JOINTS`.
  - ⏳ Stage names, scopes, route names, debounce and page-size values.
- **Complex template expressions.** ✅ `ReportActions` dot-color nested ternary → `actionColor()`. ⏳ `JointForm` triple-cell repetition; `WorkOrderBrowser` calls `actionFor()` 3× per row.

## 6. Technical debt findings

| Item | Status |
|---|---|
| `eslint.config.js` loaded the TypeScript preset into a JS project (101 `vue/block-lang` errors) | ✅ removed |
| `flat/essential` rejected Vuetify's `#item.x` slots (137 errors) | ✅ `allowModifiers: true` |
| `JointForm` mutated its `joint` prop (19 errors) | ✅ reads `report.joints[index]` from the store (same object) |
| `api.js` fetch-options lint errors | ✅ equivalent rewrite |
| `npm run lint` rewrites files | ✅ added the non-mutating `npm run lint-check` |
| Unused devDependencies (`@vue/tsconfig`, `vite-plugin-vue-devtools`, `@vue/eslint-config-typescript`) | ✅ removed |
| `Microsoft.AspNetCore.SpaProxy` floats on `10.*-*` | ⏳ pin to the version in `obj/project.assets.json` on a machine with the SDK |
| No root `.vscode/launch.json` / `tasks.json` | ✅ added |
| No `docs/production-readiness.md` (the README linked to it) | ✅ added |
| `DateTime.Now`/`Today` in ~20 places; only `IdempotencyGate` uses `TimeProvider` | ⏳ inject `TimeProvider` and use `GetLocalNow()` |
| Mixed semicolon style on the client, even within single files | ⏳ one style-only commit with `@stylistic` ESLint rules |

## 7. Performance findings (all behavior-preserving)

- **Missing `AsNoTracking`** on read-only paths: `ReportService.GetEntityAsync` for load/pdf/excel, and the Lookups export. ⏳
- **Over-fetching.** `ConsumableItemService.SpecificationNamesAsync` loads whole entities. ⏳ `Select(...).Distinct()` in SQL.
- **N+1 queries inside the stock lock:**
  - `StockCountService.cs:129-139` makes up to ~108 queries.
  - `VoidAsync` repeats per-group lookups.
  - `OutstandingAsync` makes two `SumAsync` calls.
  - ⏳ Batch them.
- **Supervisor token** decrypted 3–4× per request. ⏳ Cache it per request.
- **Indexes.** ⏳ An index-only migration:
  - Add `Reports.UpdatedAt`, `Reports(ReportRequired, CompletedAt, DateWelded)` and `ConsumableMovements.ReferenceNo`.
  - Drop `WpsItems.WpsNo`, which is redundant with the unique `(WpsNo, PNo)` index.
- **Client.**
  - `isDirty` stringifies the whole report on every edit.
  - `JointForm` rebuilds option arrays 12× per joint per render.
  - ⏳ Per-column computeds.

## 8. Security findings

| Sev | Finding | Location | Recommendation | Status |
|---|---|---|---|---|
| Med | Dev supervisor password committed | `launchSettings.json` | `dotnet user-secrets` | ✅ |
| Med | CSV formula injection: the Lookups export uses unguarded `CsvText.ToCsv` (`ConsumablePIC` values come from the unauthenticated baking endpoint), and the client `downloadCsv` is unguarded | `LookupsController.cs`, `utils/files.js` | Reuse the `CsvText.Cell` / `TraceResultsTable` guard | ⚠️ |
| Med | Data Protection keys stored unencrypted; a leaked keys folder allows forged supervisor tokens | `Program.cs` | `.ProtectKeysWithDpapi()` on Windows | ⚠️ |
| Med | Over-length input → 500 (truncation) on WPS/MRN/BPVC/ProcessTypeLinks create/update, report `WorkOrderNumber` > 100, and Lookups import > 200 | Settings controllers, `ReportsController` | Validate against max lengths → 400 | ⚠️ |
| Low | Revoked supervisor tokens are held in memory only, so they become valid again after an app-pool recycle; refresh has no absolute cap | `SupervisorAuth.cs`, `SupervisorController.cs` | Persist a revocation epoch; cap the session lifetime | ⚠️ |
| Low | No CSP/HSTS; static files skip the security headers | `Program.cs` | Reorder middleware; add CSP | ⚠️ |
| Low | Reference-table imports don't check the `.csv` extension; the Lookups import has no Latin-1 fallback or unguard | `ReferenceCsv.ReadAsync`, `LookupsController` | One shared upload reader | ⚠️ |
| Info | Reports, welders and exports are readable without a token; `X-Entered-By` is client-controlled | — | Accepted for the kiosk model; documented in the README | ✅ documented |

No SQL injection was found. `ConsumableLedger` interpolates only `const` sequence names; ⏳ a private switch would make that explicit.

## 9. Error handling findings

- **Unmapped `DbUpdateException` → 500** on every Settings create/update/delete, on `ConsumablesController.DeleteItem` (which bypasses `Locked()`), and in the `WeldersController.Create` check-then-insert race. ⚠️ Changes 500 → 409.
- **`ConsumableControllerBase`** maps every `DbUpdateException`, including truncation, to "someone else saved". The exception is logged, but the message misleads. ⚠️
- **`WorkOrdersController.Guard`** logs client-aborted requests as errors. ⏳ Filter out `OperationCanceledException`.
- **Client promises with no `catch`:** `ReferenceTable` load/delete, `LookupTable` load/reorder/export, `ProcessTypeLinkTable`, and `ReportForm`'s `lookupStore.load()`. Today these fail silently in the UI. ⚠️ Showing an error is a visible change.
- **Client catches that swallow errors:**
  - `reportStore.fetchHeader` reports a network error as "Work order not found".
  - The `bpvc`/`wps`/`processType`/`welder` store loads.
- **Missing stale-response guards:** `reportStore` `autofillFromWorkOrder`, `loadForWorkOrder`, `duplicateReport` and `loadDashboard`. The paged tables' `loadMore` can append a page that is stale after a filter change. ⏳

## 10. Testability findings

- **Clock.** Direct `DateTime.Now`/`Today` reads, including the static `ConsumableText.Today`, make the backdate window, return window, stale-draft KPI and baking times untestable. ⏳ Inject `TimeProvider` and use `GetLocalNow()`, which gives identical local-time semantics. Add characterization tests with `FakeTimeProvider`.
- **HTTP inside a domain service.** `ConsumableGuards` reads supervisor state from `HttpContext`. ⏳
- **Pure logic buried in long methods.**
  - The `StockImportService.PlanRow` parsers, the `ReportService` dashboard aggregation, and Settings validation.
  - ⏳ Extract them as DB-free functions, following the pattern `ReferenceCsv` and `ReportSaveRules` already use.
- **Client.**
  - There is no test runner.
  - The pure helpers are now isolated modules (`utils/errors.js`, `files.js`, `date.js`, `fileName.js`) and would be easy Vitest targets.

## 11. Consistency findings

- **Three API-result conventions** (`ServiceResult`, enum results + exception, raw controllers). Some Consumables actions (`ConsumableCountsController`, `ConsumableOvensController`) return raw `Ok` where their siblings use `ToAction`. ⏳
- **Request DTOs declared inside controllers** (`SupervisorController`, `WorkOrdersController`). ⏳ Move them to `Models/`.
- **Client.**
  - Semicolon style is split by folder and mixed within some files. `vite.config.js` uses 4-space indent and double quotes.
  - `useConsumableStore` is imported as `supervisor` in report components.
  - Date formatting mixes `toLocaleString()` and `en-GB` helpers.
  - ⏳ All of the above.
  - ✅ The duplicate `pad` helpers are unified in `utils/date.js`.

## 12. Documentation findings

- ✅ `docs/production-readiness.md`: the release checklist with smoke tests per module.
- ✅ README:
  - An architecture section (controller pattern, stock lock order, open kiosk endpoints, where client server calls live).
  - User-secrets for the dev password.
  - `lint-check`.
  - `DCenter__AutoMigrate` in the configuration table.
- ⏳ XML comments where they add value: `StockLocks` (lock order), `ConsumableLedger` (stage sign conventions), `ServiceResult`.

## 13. Safe refactoring opportunities

Each opportunity below is behavior-preserving by construction:

| Refactor | Why behavior is unchanged |
|---|---|
| Replace literals with constants | Identical values; C# `const` strings are inlined, so EF-generated SQL is the same. |
| Move code verbatim into helpers or services | Same statements, same order, same transaction and lock scope. |
| `useCrudApi`, `usePagedList`, `fetchReportFile` | Same URL, method, params, payload, response handling and messages. |
| Remove dead code | Unreachable or unreferenced code has no runtime effect. |
| `TimeProvider.System` + `GetLocalNow()` | Returns the same local wall-clock time as `DateTime.Now`. |
| `AsNoTracking` on read-only paths | The entities are never saved on those paths, so results are identical. |

## 14. Dead code analysis

| Item | Location | Result |
|---|---|---|
| Unreachable Normal-stage branch in `AdjustAsync` (line 468 returns for Normal, 469 requires Activated) | `ConsumableMovementService.cs` | ✅ removed |
| Null paths in `ItemTotalsAsync`/`BakingBalancesAsync` (no caller passes null) | `ConsumableLedger.cs` | ✅ removed |
| `CsvText.Parse(string)` overload | `CsvText.cs` | ✅ removed |
| `StockCatalog.MigratedReference` | `StockCatalog.cs` | ✅ removed |
| `using System.Text` | `ConsumableImportService.cs` | ✅ removed |
| `BALANCE_COLOR`, `LOW_COLOR` | `chartSetup.js` | ✅ removed |
| `ovenSearch` state | `consumableStore.js` | ✅ removed |
| `resolvedWorkOrder` (write-only) | `reportStore.js` | ✅ removed |
| `isEmpty`, unused `watch` import | `JointForm.vue` | ✅ removed |
| Unused destructured `unlock`/`next` | `SupervisorSession.vue` | ✅ rewritten |
| `CsvText.ToCsv/Quote/ReadRowsAsync/Field` | `CsvText.cs` | Kept. Only Lookups uses them, and porting Lookups adds the formula guard ⚠️. |
| `StockCatalog.TxnDispose` | `StockCatalog.cs` | Kept. It feeds a DB check constraint. |
| `ReportDto.JobNumber` | `Models/Dtos.cs` | Kept. It is part of the JSON contract. |
| `reportStore.filteredRows` (identity getter) | `reportStore.js` | Kept. It is used in 5 places; inlining it is churn with no benefit. |

## 15. Risk assessment

- **This pass: low risk.**
  - Tooling, constants with identical values, dead code, and client code moves verified by lint plus a production build.
  - The C# edits are mechanical, but the .NET SDK was not available in the environment that made them. **Run `dotnet build` and `dotnet test` before merging.**
- **⏳ Deferred work: medium risk.**
  - It touches the stock ledger (the `ConsumableMovementService` split, `TimeProvider`).
  - Do it one step at a time, with the test suite plus characterization tests. Diff the CSV/PDF/Excel outputs from a staging DB before and after, and walk through `docs/production-readiness.md`.
- **⚠️ items: low technical risk, but they change what users or clients see** (status codes, messages, CSV cell text, headers). Each needs explicit sign-off.

## 16. Recommendations ranked by priority

1. ✅ Make lint pass and non-mutating.
2. ✅ Remove the committed dev password. ⏳ Pin SpaProxy.
3. ✅ Remove verified dead code.
4. ⚠️ Map `DbUpdateException` and over-length input to 409/400 instead of 500.
5. ⚠️ Add formula-injection guards on the Lookups and client CSV exports.
6. ✅/⏳ Centralize constants (partly done).
7. ⏳ Inject `TimeProvider`; decouple `ConsumableGuards` from `HttpContext`.
8. ⏳ Move the Settings controllers onto services + `ServiceResult`.
9. ⏳ Split `ConsumableMovementService`, `StockImportService.PlanRow` and `ReportService`.
10. ⏳ Consolidate the CSV header mapping and upload readers.
11. ⏳ Extract `supervisorStore` from `consumableStore`, and `traceStore`/`reportDashboardStore` from `reportStore` (keep facades until every caller has moved).
12. ⏳ Performance: `AsNoTracking`, the N+1 queries in stock count and void, the per-request token cache, an index-only migration.
13. ⚠️ Protect the Data Protection keys with DPAPI, move static files behind the security headers, and persist token revocation.
14. ⏳ Unify client code style in one mechanical commit.
