# DCenter — Production Readiness Assessment

**Scope:** Report and Consumables modules, server, client and database design.
**Method:** line-by-line code review of every service, the ledger, locking, constraints, auth, startup and the client submit flows. This was followed by fixes, verified with:
- `dotnet build` (0 warnings);
- `dotnet test` (69/69);
- EF migration drift check (clean);
- a mocked-API browser smoke test (40/40).

**Not verified:** the database-backed integration tests (section 9) were **not** run against SQL Server. None was available in the review environment. Run them on staging before go-live.

## 1. Production readiness assessment
**Verdict at review time: NOT READY.** The blockers found are fixed in code (see section 7). The remaining go-live conditions are in section 15.

**Verified strengths:**
- **Ledger design.** Stock balances are derived from an immutable movement ledger (`ConsumableLedger`); no stored balance can drift. Voids are compensating entries with a unique `VoidsMovementId`.
- **Locking.** Every stock mutation serialises on `sp_getapplock` (per item, plus a per-compartment lock in `CheckCompartmentAsync`, plus a master lock for receive and imports). Balances are re-read inside the lock, so there are no double deductions or negative balances under concurrency.
- **Database constraints.** Check constraints cover stage, qty > 0, bin ↔ stage, hold/move shape, statuses and baking time order.
- **Stock count.** Stale count sheets are detected (409 when stock moved since the sheet was loaded).
- **CSV and passwords.** CSV export guards against formula injection. Supervisor passwords use PBKDF2-SHA256 with 210k iterations.
- **Supervisor enforcement.** Supervisor-only endpoints are enforced server-side (`SupervisorOnlyFilter`), not only by the client router.

**Weak areas:**
- **Report module.** No server-side state machine, no auth, and optimistic concurrency only on one path.
- **Duplicate submissions.** No protection beyond a client in-flight flag.
- **Secrets.** Committed to the repo.
- **Tests.** Zero automated tests.
- **Operations.** No production error handling or logging.

## 2. Critical issues
| ID | Issue | Evidence | Impact |
|---|---|---|---|
| C1 | Production SQL credentials (server, DB, user, **password**) committed in `DCenter.Server/appsettings.json`, present in 8 commits of history | `ConnectionStrings:DefaultConnection` | Anyone with repo access can reach the production DB. Violates the env-var-only rule. |
| C2 | **Silent overwrite / lost update on report save.** `ReportService.SaveAsync` applies the RowVersion check only when `dto.RowVersion` is non-empty. New reports and duplicates send `null`, so if a report already exists for that WO, it is replaced wholesale (all joints deleted and re-inserted). | `ReportService.cs` SaveAsync; `reportStore.newReport` / `duplicateReport` set `rowVersion: null` | Two users start the same WO, or someone duplicates into a WO that already has a report, and the first user's QA record is lost without warning. |
| C3 | **Completed (signed-off) reports are editable.** SaveAsync never checks `CompletedAt`. The client form has no read-only state and the Save button stays active. Complete, Reopen and Delete have no authorization at all. | `ReportsController` (no `[SupervisorOnly]`); `ReportForm` / `JointForm` | Controlled QA documents can be altered after completion by anyone on the network. The leave-guard skips completed reports, so edits are also silently lost or saved. |
| C4 | **No automated tests of any kind** (no test project in `DCenter.slnx`) for a stock ledger with FIFO allocation, voids and baking state machines | repo | Every change ships unverified; regressions are only found in production. |

## 3. High-risk issues
| ID | Issue | Evidence |
|---|---|---|
| H1 | **False "changed by someone else" conflict after Complete or Reopen.** `/complete` updates the row, which changes its RowVersion. The client keeps the old RowVersion (`setComplete` only calls `markPristine`), so the next Save fails with 409 and the user must reload and lose edits. | `reportStore.setComplete`, `ReportService.MarkCompleteAsync` |
| H2 | **PDF electrode header is hard-coded `GTAW / - / -`** regardless of the processes entered, so the printed report misstates the process. PDF/Excel are generated from the *saved* state with no warning when there are unsaved edits. | `PdfReportService.ElectrodeTable`; `ReportActions` |
| H3 | **Excel card omits per-joint Part No.** (left and right). Sign-off Date cells for Engineer/Supervisor and QA Inspector are auto-filled with Date Welded, misrepresenting sign-off dates. The names are never captured. | `ExcelReportService.JointBlock` |
| H4 | **No idempotency on stock transactions.** The client in-flight flag stops double-clicks. A browser resubmit, a proxy/IIS retry, or a user retrying after a hung request creates a duplicate Receive, Issue, Return, Place, Send-to-bake, Move, Finish, Void or stock count. **Re-uploading the same opening-stock CSV doubles stock** (no batch or duplicate check against existing `OPENING` receipts). | `ConsumableControllerBase.Locked`; `StockImportService` |
| H5 | **Report "today" uses UTC** (`new Date().toISOString().slice(0,10)`). Between 00:00 and 07:59 MYT, Date Welded defaults to *yesterday* on new and duplicated reports. | `reportStore.js:45, :582` |
| H6 | **Unauthenticated identity on the shop floor.** The `X-Entered-By` header and `WelderId` in the body are client-supplied, so anyone on the network can issue, return, finish, bake or place as any welder. The Report module has no auth. Audit `CreatedBy` is therefore not trustworthy. *(Accepted risk for a kiosk model; mitigate in Phase 2.)* | `ConsumableControllerBase.EnteredBy` |
| H7 | **No production error handling or logging.** There is no `UseExceptionHandler`. `Locked` only maps `TimeoutException`. Any `DbUpdateException` becomes a bare 500: for example, `EnsureLookupsAsync` unique-index races when two item-locked transactions add the same new brand or person-in-charge, or a failing report update. Report insert maps *every* `DbUpdateException` to "created by someone else" (misleading). Logs go only to console, which is invisible on IIS. | `Program.cs`, `ReportService.SaveAsync` |

## 4. Medium-risk issues
- **M1 Supervisor tokens.**
  - Sliding renewal (added last round) makes a token's lifetime unbounded.
  - Logout is client-only, so a copied token stays valid.
  - There is no idle logout (user's choice) on shared terminals.
  - → Add an absolute cap (e.g. 7 days from login) and server-side revocation on logout.
- **M2 Login brute force.** There is only a 1 s delay per failure; parallel attempts are unlimited and there is no lockout.
- **M3 Unlimited back-dating.** Balance checks are all-time, not as-of the transaction date, so history can show negative stock on past dates and monthly dashboards shift after the fact.
- **M4 "Complete" has no content validation.** Joints without WPS, welder or heat no. can be completed.
- **M5 Client hangs.** The client `fetch` has no timeout, so a hung request spins forever and invites a refresh-and-resubmit (feeds H4).
- **M6 Stale screens.** There is no refresh on the oven board or welder station, so two stations show different balances. The server rejects over-draws, so integrity holds; this is UX only.
- **M7 Work-order list at scale:**
  - The WO search without a term groups the entire `Work_Order_Detail`.
  - Duplicate mode loads **every** WO number into the browser (`/workorders/numbers`).
  - Both are slow at 50k+ rows.
- **M8 BOM levels.** Each BOM level query scans the unindexed `Bill_Of_Material_Others`, and the client caps trees at 5,000 nodes.
- **M9 Startup migrations.** Auto-migration runs at startup, and IIS overlapped recycling can start two processes migrating at once. Migrations DCenter11–15 were hand-authored with synthetic timestamps and are unverified against the snapshot.
- **M10 Data Protection keys.** They live in `App_Data/keys` under the content root; "delete existing files" on publish wipes them and ends all sessions.
- **M11 Settings imports.** The BPVC/WPS/MRN/Lookup CSV imports have no `RequestSizeLimit` (the 30 MB default) and load whole tables into memory.
- **M12 Report list.** `/api/reports` is unpaged.

## 5. Low-risk issues
- **L1 Joint numbering.** `(ReportId, JointNumber)` is not unique, and more than 50 joints are truncated server-side without an error.
- **L2 FinishDialog.** `FinishDialog.submit` lacks the `saving` guard at function entry (the button's loading state mitigates it).
- **L3 WO numbers with special characters.** WO numbers containing `/` or `%` break `{workOrderNumber}` routes.
- **L4 PDF logo.** `PdfReportService.LogoPath` is unused. The asset name case (`Emerson.png` vs `emerson.png`) breaks on case-sensitive hosts.
- **L5 Local server time.** `DateTime.Now` is used throughout; fine for one site, but document it.
- **L6 Deleting a draft.** It cascades away its status history, leaving no audit of the deletion.
- **L7 Leftover fields.** `ReportDto.JobNumber` is unused, and `EngineerSupervisor` / `QaInspector` are never set.
- **L8 Hardening.** `AllowedHosts: *` and no security headers (acceptable on an internal network; harden later).

## 6. Root cause analysis
1. **Concurrency was designed for "edit an existing row" only.** Insert-or-overwrite paths (C2) and state changes (H1) were not modelled.
2. **The Report module has no server-side state machine** (Draft → Completed → Reopened) and no authorization; the client is trusted (C3).
3. **There is no request-identity layer.** Correctness relies on the UI not sending twice (H4).
4. **Secrets and configuration.** The env-var convention was documented but not enforced on the committed `appsettings.json` (C1).
5. **No test harness or CI,** so regressions such as H1, H2 and H5 were never caught (C4).
6. **Operational readiness** (logging, error mapping, deployment of keys and migrations) was not part of the definition of done (H7, M9, M10).

---

## 7. Remediation status (2026-09-28)

| ID | Status | What changed |
|---|---|---|
| C1 | **Fixed in code — action needed** | Connection strings in `appsettings*.json` are now empty. The server refuses to start without `ConnectionStrings__DefaultConnection`. **Rotate the DB password.** It remains in git history until you rewrite it. |
| C2 | **Fixed** | `ReportSaveRules.Conflict` refuses a save onto an existing report without its id and RowVersion. Duplicate mode warns and blocks when the chosen WO already has a report. |
| C3 | **Fixed** | Server: saving a completed report returns 409, and reopen and delete need a supervisor. The reopen event records who did it. Client: completed reports are read-only, with no Save, a "Locked" chip, and Reopen for supervisors only. |
| C4 | **Fixed** | Added a `DCenter.Server.Tests` project with 69 tests, all passing. |
| H1 | **Fixed** | After Complete or Reopen, the client reloads the RowVersion, so the next save works. |
| H2 | **Fixed** | The PDF electrode header shows columns 1/2/3 instead of the hard-coded GTAW. PDF and Excel are blocked while there are unsaved edits. |
| H3 | **Fixed** | The Excel joint description includes part numbers. Engineer/Supervisor and QA Inspector names and dates are blank for wet-ink signing; the welder date stays. |
| H4 | **Fixed** | Idempotency keys on every consumable mutation: a server `IdempotencyGate` plus a per-form `useSubmitKey`, keyed by a fingerprint of the request. Re-imported opening-stock rows are rejected. **Limit:** keys are held in memory, so an app-pool recycle between a request and its retry is not covered. |
| H5 | **Fixed** | The report date uses the local day (`utils/date.js`). |
| H6 | Open (accepted risk) | Kiosk identity model unchanged. See Phase 2. |
| H7 | **Fixed** | ProblemDetails, the exception handler and status-code pages are on, with Windows Event Log logging. DB update conflicts in consumables return 409 and are logged. The report insert conflict is detected only by SQL unique-violation errors. |
| M5 | **Fixed** | Client requests time out after 60 s (downloads 120 s) with a "check today's entries" message. |
| M9 | **Verified** | `dotnet ef migrations has-pending-model-changes` reports that the migrations match the model. |
| M1–M4, M6–M8, M10–M12, L1–L8 | Open | Phase 2/3 backlog. See the plan below. |

### Phase 2 / 3 backlog
- **M1** An absolute token cap (e.g. 7 days) and server-side revocation on logout.
- **M2** A login rate limiter.
- **M3** A back-dating window with supervisor override.
- **M4** Completion validation: joints need WPS, welder and heat no.
- **H6** Welder PIN or kiosk binding.
- **M6** Auto-refresh of the station and oven board.
- **M7** WO search requires 2+ characters, and a server lookup replaces the full WO number list.
- **M8** A BOM copy table if large expansions stay slow.
- **M9** Set `DCenter__AutoMigrate=false` in production and migrate in the deploy step.
- **M10** Keys folder outside the site.
- **M11** Size limits on the settings imports.
- **M12** Paged report list.
- **L1–L8** as listed above.

## 8. Unit test plan
Tests marked ★ are implemented in `DCenter.Server.Tests` (69 tests, all passing).

**Consumables**
- ★ `ConsumableLedger.AllocateFifo`: exact fill; spans lots in LotId order; skips ≤ 0 lots; insufficient → null; qty 0.
- ★ `ConsumableLedger.DeriveStatus`: every path (not sent → Cancelled; Queued / Baking / Baked / Closed; rebake Queued / Rebaking / Rebaked / Closed; balance 0 vs > 0).
- ★ `ConsumableGuards.Take`: specific lot within / over the available qty; FIFO path; error text totals ignore negatives.
- ★ `ConsumableGuards.ResolveBin`: electrode with bin; filler with bin → error; filler without bin.
- ★ `ConsumableGuards.Common`: missing user; future date; default today.
- ★ `ConsumableText`:
  - `RoundKg` midpoint away from zero.
  - `Diameter` handles "3.2mm" → "3.20", "3,2", 0 and ≥ 100 rejected, mesh "100/325" only for non-electrodes.
  - `MatchOption` exact / suggestion / ambiguous.
  - `FreeText` truncation and whitespace collapse.
- ★ `CsvText`:
  - `Parse` handles quoted commas, escaped quotes, CRLF, blank lines skipped, and an unclosed quote error with its line number.
  - `Write` guards `=cmd` → `'=cmd` and leaves `-5.00` alone.
  - `Unguard` round-trip.
- ★ `SupervisorAuth` (`EphemeralDataProtectionProvider`): issue → validate name; tampered token → null; `RevokeIssuedBefore` invalidates older tokens.
- ★ `IdempotencyGate`: the same key runs once and a concurrent duplicate gets the same result; failure is not cached; a different user or route with the same key is not shared; expiry.
- DB-backed, needs SQL Server; to add with a `DCENTER_TEST_SQL` connection string, skipped when absent:
  - Receive / Transfer / Issue / Return / Send-to-bake / Place / Move / Finish / Adjust / Void validation and balances.
  - Over-return window.
  - Rebake-once rule.
  - Compartment exclusivity.
  - Void blocked when downstream stock was consumed.
  - Stock count stale-sheet 409.

**Report**
- ★ Save conflict rules (pure helper extracted from SaveAsync): exists + no RowVersion → conflict; completed → conflict; new → insert.
- Excel/PDF (DB-free, build a `Report` entity in memory):
  - PDF generates bytes for 0, 1 and 50 joints with null fields.
  - Excel cells: Part No. in the joint description; sign-off dates blank; welder date equals Date Welded.
- Validation: missing WO or DateWelded → 400.

## 9. Integration test plan (staging SQL Server)
**Report**
- Search WO → header → BOM children → start report (1 joint) → fill → save → reload (fields round-trip, P# autofill, WPS filter).
- Complete → reopen (supervisor) → edit → save (no false conflict).
- PDF and Excel field-by-field against the saved record.

**Consumables chain**
- Receive 10 kg lot L1 → Normal 10.
- Send-to-bake 6 → Normal 4, Baking 6 (record Queued).
- Start/Stop → Baked.
- Place 4 in AS-1 and 2 finished-after-baking to welder W → Activated AS-1 4, record Closed, holding records ×2.
- Issue 3 to W → AS-1 1.
- Return 1 → AS-1 2.
- Finish 2 (welder, ≤ threshold) → 0.
- Void each step in reverse → balances restore exactly. Voiding out of order is blocked with 409.

**Bare & powder chain:** Receive → Transfer Normal → Activated → Issue → Return → Finish; stock-count gain and loss.

**Oracle views:** the BOM view returns grouped children; the WO header matches `Work_Order_Detail`.

## 10. System test plan
- **Report E2E:** 3 real WOs (small, 20-joint and deep BOM).
  - Compare the printed PDF and Excel against the form: every field, joint order, processes per column, P#, WPS and welder.
  - No truncation of long descriptions.
- **Consumables E2E:** one full day of simulated operations across 3 stations. At end of day:
  - Dashboard totals = Σ(balances).
  - Oven board = Σ(Activated compartments).
  - Transactions audit shows every step with user and time, and no orphans: every movement has a lot, every `BakingRecordId` resolves, and every holding `TxnNo` matches movements.
- **Recovery:** kill IIS mid-transaction (the transaction rolls back); restart (migrations are a no-op); existing sessions survive when keys persist.

## 11. Acceptance test plan
- **Welder:**
  - Select self → pick up electrodes from a compartment in ≤ 3 taps.
  - Return excess.
  - Mark a leftover used-up.
  - Place a baked batch into a compartment.
  - Error messages are understandable.
- **Supervisor:**
  - Receive with a new lot.
  - See low stock on the dashboard.
  - Import an opening CSV (preview → fix → commit; a re-import is rejected).
  - Export the item master.
  - Run a stock count.
  - Void a mistaken entry.
- **Report clerk:**
  - Find a WO by typing a partial number.
  - Start a report.
  - Fill joints using P#-filtered WPS.
  - Save, complete, and print the PDF.
  - Reopen needs a supervisor.
- **Sign-off criteria:** no blocker defects; all critical workflows pass with 2 real users per role.

## 12. Regression checklist (run before each release)
- [ ] **Report:**
  - [ ] Search (partial and exact WO, Enter to open).
  - [ ] Start report (1 joint, local date).
  - [ ] Save / reload round-trip.
  - [ ] Duplicate into an existing WO is blocked.
  - [ ] Two-browser concurrent save → second gets 409.
  - [ ] Complete / Reopen without a false conflict.
  - [ ] A completed report is read-only.
  - [ ] PDF process headers and Excel part numbers.
  - [ ] Saved list All / Drafts / Completed.
  - [ ] Per-row PDF.
  - [ ] Dashboard KPIs.
  - [ ] Traceability search and CSV.
- [ ] **Consumables:**
  - [ ] Receive / Transfer / Issue / Return / Finish / Adjust / Void balances.
  - [ ] Baking queue → start → stop → place.
  - [ ] Holding compartment exclusivity.
  - [ ] Rebake once only.
  - [ ] Stock count, including stale-sheet 409.
  - [ ] Dashboard totals = balances.
  - [ ] CSV item import/export round-trip.
  - [ ] Opening import, with re-import rejected.
  - [ ] Idempotent retry: the same key replays and creates no new TxnNo.
- [ ] **Security:** supervisor-only APIs return 401 without a token; direct URLs to supervisor pages redirect; logout clears the session; password change revokes old tokens.
- [ ] **Build:** `dotnet build`, `dotnet test`, client lint (no new errors vs baseline) and build.

## 13. Performance test plan
- **Seed:**
  - 1k / 10k / 50k movements over 200 items and 2,000 lots, 5k baking records, 2k reports with 20 joints each.
  - WO view with 50k WOs; BOM with 200k links.
  - Use a T-SQL seed script on staging.
- **Measure p95** (server-timing via logs) for:
  - WO search (term / no term).
  - BOM expand (3 and 8 levels).
  - `/consumables/balances`, `/counter`, `/ovens`, `/dashboard`.
  - `/transactions` pages.
  - Stock-count sheet.
  - Opening import of 5,000 rows (preview and commit).
  - Report save with 50 joints, and PDF/Excel of 50 joints.
- **Targets** for a small internal app: reads < 1 s, dashboard < 2 s, import commit < 15 s, PDF < 3 s.
- **Known hotspots to watch:**
  - Ledger aggregates recompute from all movements on every call (covered by indexes with INCLUDE; watch at 50k+ and add a nightly balance snapshot if p95 > 1 s).
  - `ActivatedBinsAsync` runs 2 aggregates per call.
  - `BalancesAsync` runs 3 full aggregates per call. There are no N+1 query loops in the consumables services.
  - WO search with no term (M7).
  - Client rendering: virtual tables are used everywhere; check the oven board with 36 compartments × many lots.

## 14. Security test plan
- **Supervisor bypass:** call every `[SupervisorOnly]` endpoint without a token, with an expired token, with a tampered token, and with a token issued before a password change → 401 each time.
- **Direct URL:** `/consumables/dashboard`, `/settings` and `/print/...` without a login → redirect with the unlock prompt.
- **Report authorization:** reopen or delete without a supervisor → 401/403. Saving a completed report → 409.
- **Input validation:**
  - Oversize strings (remarks > 500 are truncated server-side).
  - Negative, zero and over-max qty.
  - Future dates.
  - Unknown enums.
  - WO numbers with `/ % ' --`.
  - SQL injection attempts on every search `q` (EF parameterises; verify).
- **CSV uploads:**
  - A 2 MB+ file → 413 / 400.
  - Non-UTF8 (Latin-1 fallback).
  - Formula cells `=HYPERLINK(...)` are preserved as text on import and guarded on export.
  - 5,001 rows → rejected.
  - An unclosed quote → a clear error.
- **Files:** PDF/Excel are generated from DB values only, with no path input. The download filename is sanitised client-side, and the server filename uses the WO → verify there is no header injection.
- **Secrets:** confirm no credentials in the repo or the published output; env vars are set at machine level.
- **Brute force (M2):** measure attempts per second against `/supervisor/login`.

## 15. Go-live readiness verdict
**Before these fixes: NO-GO.**
**Now: conditional GO.** Every critical and high-risk defect except H6 is fixed in code, builds cleanly and is covered by unit and smoke tests.

**Go-live conditions, all still open:**
1. **Rotate the database password** and set it only via `ConnectionStrings__DefaultConnection` (C1).
2. Deploy the server and client together, and re-run `DCenter_SourceViews.sql` on the DCenter database.
3. Run the section 9 integration tests and the section 12 regression checklist on a **staging SQL Server**. They could not run in the review environment.
4. Two real users per role complete the section 11 acceptance scenarios.

**Accepted risks for the initial internal rollout (Phase 2):** H6 (kiosk identity) and the open M/L items in section 7.
