# BUGS — SiteQueryDefectTracking

Bug log maintained during QA. Each bug gets a BUG ID, status (OPEN/FIXED/VERIFIED), severity, priority, root cause, affected file, and fix.

---

## BUG-001 — Query creation always fails with 401 for authenticated users (CRITICAL / P0)

- **MODULE:** Queries (POST /api/queries)
- **SEVERITY:** Critical
- **PRIORITY:** P0
- **STEPS TO REPRODUCE:**
  1. Login as manager or site engineer.
  2. `POST /api/queries` with a valid `projectId`, `issueTypeId`, `ipo`, `quantityNos`, `quantitySqm`.
  3. Response: `401 {"success":false,"message":"Authentication is required."}`
- **EXPECTED RESULT:** 200 with new query id.
- **ACTUAL RESULT:** 401 for every authenticated caller; only anonymous calls pass the guard (but are blocked by `[Authorize]`).
- **ROOT CAUSE:** Inverted condition in `QueryService.CreateAsync`:
  ```csharp
  if (!string.IsNullOrWhiteSpace(currentUser.UserId))
      throw new UnauthorizedException();
  ```
  Throws when the user IS authenticated.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/QueryService.cs:31-32`
- **FIX:** Inverted `!` removed — guard now throws only when `UserId` is blank.
- **STATUS:** FIXED / VERIFIED (create query returns 200 for manager + engineers; harness 42/42)

---

## BUG-002 — Resolve records wrong FromStatus in status history (HIGH / P1)

- **MODULE:** Queries (PUT /api/queries/{id}/resolve)
- **SEVERITY:** High
- **PRIORITY:** P1
- **STEPS TO REPRODUCE:**
  1. Create/obtain an open query (status Pending or InProgress).
  2. Manager resolves it.
  3. Inspect `statusHistory`: entry shows `fromStatus=Resolved, toStatus=Resolved`.
- **EXPECTED RESULT:** history entry `fromStatus` = previous status (e.g. Pending/InProgress), `toStatus=Resolved`.
- **ACTUAL RESULT:** `fromStatus` is `Resolved` because it is read **after** `entity.Status` is set to `Resolved` (line 241).
- **ROOT CAUSE:** `ResolveAsync` builds history after mutating `entity.Status`:
  ```csharp
  entity.Status = QueryStatus.Resolved;           // line 241
  ...
  FromStatus = entity.Status,                     // line 250 -> always Resolved
  ```
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/QueryService.cs:241-255`
- **FIX:** Capture `var fromStatus = entity.Status;` before assigning `Resolved`; history now uses the captured value.
- **STATUS:** FIXED / VERIFIED (history shows `Pending -> Resolved`)

---

## BUG-003 — Most FluentValidation validators are never executed (HIGH / P1)

- **MODULE:** Cross-cutting (validation)
- **SEVERITY:** High
- **PRIORITY:** P1
- **STEPS TO REPRODUCE:**
  1. `POST /api/reports/generate` with `{"type":99,"format":"Csv"}` (invalid enum value) → returns 200 CSV.
  2. Same with reversed `from`/`to` date range → returns 200.
- **EXPECTED RESULT:** 400 validation errors.
- **ACTUAL RESULT:** Invalid payloads accepted; `ReportRequestValidator` (with `IsInEnum()` + date-range rule) is registered in DI but never invoked.
- **ROOT CAUSE:** Validators are registered via `AddValidatorsFromAssembly` but only two are invoked manually (`CreateQueryRequestValidator`, `CommentRequestValidator`). There is no automatic FluentValidation pipeline (no MediatR/validation filter). Validators for login, change-password, create/update user, reset-password, product, email template/send, report are dead code.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Validators/*.cs` (unused); `src/SiteQueryDefectTracking.Application/Services/*.cs` (validators not invoked)
- **FIX:** Added `FluentValidationActionFilter` registered globally in `Api/Common/Configuration.cs`; resolves `IValidator<T>` per action argument and throws app `ValidationException` on failure. Report type 99 / reversed date range now 400; login empty body/missing password now 400 (was previously a 401-only boundary).
- **STATUS:** FIXED / VERIFIED (report invalid type + reversed range -> 400)

---

## BUG-004 — Non-managers can view ALL queries via dashboard and search bypass (HIGH / P1)

- **MODULE:** Dashboard + Queries search
- **SEVERITY:** High
- **PRIORITY:** P1
- **STEPS TO REPRODUCE:**
  1. Login as Site Engineer.
  2. `GET /api/dashboard/open` → returns all open queries from all engineers.
  3. `GET /api/queries?mineOnly=false` or `GET /api/queries?raisedByUserId=<other-engineer-id>` → returns other engineers' queries.
- **EXPECTED RESULT:** Engineers only see their own queries; dashboard restricted.
- **ACTUAL RESULT:** `CanViewDashboard` policy = `RequireAuthenticatedUser` grants dashboard (all queries) to any role; `SearchAsync` scoping is bypassed when `MineOnly == false` or when `RaisedByUserId` is provided.
- **ROOT CAUSE:** `Api/Common/Configuration.cs:75` (policy); `Application/Services/QueryService.cs:106` (scoping condition); `DashboardService` has no user scoping.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Api/Common/Configuration.cs`, `src/SiteQueryDefectTracking.Application/Services/QueryService.cs`, `src/SiteQueryDefectTracking.Application/Services/DashboardService.cs`
- **FIX:** `CanViewDashboard` policy tightened to `RequireRole(Manager)`; `ProjectsController` moved to plain `[Authorize]` (reference data needed by engineers); `SearchAsync` now always scopes non-managers to `RaisedByUserId == currentUser.UserId` regardless of `MineOnly`/`RaisedByUserId` params.
- **STATUS:** FIXED / VERIFIED (engineer dashboard/open -> 403)

---

## BUG-005 — AddComment / GetComments do not enforce access (IDOR) (HIGH / P1)

- **MODULE:** Queries comments
- **SEVERITY:** High
- **PRIORITY:** P1
- **STEPS TO REPRODUCE:**
  1. Engineer A creates a query.
  2. Engineer B calls `POST /api/queries/{A-query}/comments` or `GET /api/queries/{A-query}/comments`.
- **EXPECTED RESULT:** 403 — Engineer B has no access to Engineer A's query.
- **ACTUAL RESULT:** 200 — comments can be read/added on any query by any authenticated user (no `EnforceAccess` in comment methods; `GetAsync`/`UpdateAsync` do enforce it).
- **ROOT CAUSE:** `QueryService.AddCommentAsync` / `GetCommentsAsync` lack access enforcement.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/QueryService.cs:268-317`
- **FIX:** Added `EnforceAccess(entity)` to `AddCommentAsync` and `GetCommentsAsync`.
- **STATUS:** FIXED / VERIFIED (engineer GET/comment on other engineer's query -> 403)

---

## BUG-006 — SMTP options misconfigured (config keys mismatch) (MEDIUM / P2)

- **MODULE:** Email
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:**
  1. `appsettings.json` sets `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpUsername`, `Email:SmtpPassword`.
  2. `EmailOptions` binds `Host`, `Port`, `UserName`, `Password`.
- **EXPECTED RESULT:** SMTP settings bound and `IsConfigured == true`.
- **ACTUAL RESULT:** `Host` is null → `IsConfigured == false` → emails never actually sent (logged as "dev-mode (SMTP not configured)").
- **ROOT CAUSE:** Configuration keys don't match option property names.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Api/appsettings.json:28-33`, `src/SiteQueryDefectTracking.Application/Services/EmailService.cs:213-221`
- **STATUS:** OPEN

---

## BUG-007 — Audit logs never store username (MEDIUM / P2)

- **MODULE:** Audit
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:** Any audited action (login, create, etc.) → query `api/audit`.
- **EXPECTED RESULT:** `username` populated.
- **ACTUAL RESULT:** `username` is always `null`.
- **ROOT CAUSE:** `AuditLogService.RecordAsync` hard-codes `Username = null`.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/AuditLogService.cs:17`
- **STATUS:** OPEN

---

## BUG-008 — SignalR hub is anonymous and broadcasts to all clients (MEDIUM / P2)

- **MODULE:** Realtime
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:** Connect to `/hubs/queries` with no token; receive events for queries from all users.
- **EXPECTED RESULT:** Only authenticated users; scoped events.
- **ACTUAL RESULT:** Hub mapped without `RequireAuthorization()`; `SignalREventPublisher` sends to `Clients.All`.
- **ROOT CAUSE:** `Api/Common/Configuration.cs:123` (`MapHub<QueriesHub>` no authorization), `Api/Services/SignalREventPublisher.cs`.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Api/Common/Configuration.cs`, `src/SiteQueryDefectTracking.Api/Services/SignalREventPublisher.cs`
- **STATUS:** OPEN

---

## BUG-009 — Secrets committed in appsettings.json (MEDIUM / P2)

- **MODULE:** Configuration/security
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:** Inspect `src/SiteQueryDefectTracking.Api/appsettings.json`.
- **EXPECTED RESULT:** No secrets in source control.
- **ACTUAL RESULT:** SA password, JWT signing key, seed password are hard-coded.
- **ROOT CAUSE:** Dev values committed.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Api/appsettings.json:3,8,13`
- **STATUS:** OPEN

---

## BUG-010 — Decimal QuantitySqm lacks precision configuration (LOW / P3)

- **MODULE:** Database
- **SEVERITY:** Low
- **PRIORITY:** P3
- **STEPS TO REPRODUCE:** Watch startup logs: "No store type was specified for the decimal property 'QuantitySqm'... values will be silently truncated".
- **EXPECTED RESULT:** Explicit precision/scale for QuantitySqm.
- **ACTUAL RESULT:** default `decimal(18,2)`; larger values silently truncated.
- **ROOT CAUSE:** No `HasPrecision` in `ConfigureQueries`.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Infrastructure/Persistence/AppDbContext.cs`
- **STATUS:** OPEN

---

## BUG-011 — Report "PDF" format returns HTML with misleading filename (MEDIUM / P2)

- **MODULE:** Reports
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:** `POST /api/reports/generate` with `format: Html` (the only non-Excel/CSV format) → returns `text/html` content named `*.pdf.html`.
- **EXPECTED RESULT:** Either a real PDF or a clear HTML report name.
- **ACTUAL RESULT:** HTML content with `*.pdf.html` filename — misleading.
- **ROOT CAUSE:** `ReportService.HtmlFrom` names the result `$"{data.Title}.pdf.html"`.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/ReportService.cs:173`
- **STATUS:** OPEN

---

## BUG-012 — `role` filter on user search is ignored (LOW / P3)

- **MODULE:** Users
- **SEVERITY:** Low
- **PRIORITY:** P3
- **STEPS TO REPRODUCE:** `GET /api/users?role=Manager` returns all users.
- **EXPECTED RESULT:** Only users with the Manager role.
- **ACTUAL RESULT:** `role` param accepted by controller but never used in `UserService.SearchAsync`.
- **ROOT CAUSE:** Filter missing.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Infrastructure/Identity/UserService.cs:27-55`
- **STATUS:** OPEN

---

## BUG-013 — Product create/update set Name from Description (no name field) (LOW / P3)

- **MODULE:** Products
- **SEVERITY:** Low
- **PRIORITY:** P3
- **STEPS TO REPRODUCE:** `POST /api/products` with only `description` → product `name` == description.
- **EXPECTED RESULT:** A proper product name field.
- **ACTUAL RESULT:** `Name = request.Description`.
- **ROOT CAUSE:** `ProductService.CreateAsync/UpdateAsync`.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/ProductService.cs:73,104`
- **STATUS:** OPEN

---

## BUG-014 — NotificationWriter is not wired to events; no notifications ever created (MEDIUM / P2)

- **MODULE:** Notifications
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:** Create/change/resolve a query; then `GET /api/notifications` → always empty.
- **EXPECTED RESULT:** Notifications generated from query events.
- **ACTUAL RESULT:** `NotificationWriter` is defined but never registered or invoked; events go to `NoOpDomainEventPublisher`/`SignalREventPublisher` (which only broadcasts to SignalR, and the hub is anonymous anyway). In-app notifications are effectively dead.
- **ROOT CAUSE:** No wiring between domain events and `NotificationWriter`.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/NotificationService.cs:46-73`, `src/SiteQueryDefectTracking.Infrastructure/DependencyInjection.cs`
- **STATUS:** OPEN

---

## BUG-015 — Attachment upload/download endpoints do not exist; DTO references dead URLs (MEDIUM / P2)

- **MODULE:** Files
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:** GET a query detail → each attachment has `downloadUrl = /api/queries/{id}/attachments/{id}/download`; requesting that URL → 404/405 (route not mapped).
- **EXPECTED RESULT:** Upload and download endpoints working.
- **ACTUAL RESULT:** No controller endpoints exist for attachments.
- **ROOT CAUSE:** Feature stubbed in DTO only.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/QueryService.cs:405-408` (DTO URL), controllers
- **STATUS:** OPEN

---

## BUG-016 — FileStorage LocalRoot config key mismatch (LOW / P3)

- **MODULE:** Storage
- **SEVERITY:** Low
- **PRIORITY:** P3
- **STEPS TO REPRODUCE:** `Storage:LocalRoot=assets/uploads` in config; `StorageOptions.RootPath` binds nothing → files land in `<base>/uploads`.
- **EXPECTED RESULT:** Files stored under configured root.
- **ACTUAL RESULT:** Config key `LocalRoot` ≠ option property `RootPath`.
- **ROOT CAUSE:** Key mismatch.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Infrastructure/Services/FileStorageService.cs:8-14`
- **STATUS:** OPEN

---

## BUG-017 — Slab feature is dead (SlabId never persisted) (LOW / P3)

- **MODULE:** Queries (Slab)
- **SEVERITY:** Low
- **PRIORITY:** P3
- **STEPS TO REPRODUCE:** Create query with `slabId` → reload → `slabId` always null.
- **EXPECTED RESULT:** Slab linkage persisted.
- **ACTUAL RESULT:** `Query` entity has no `SlabId` property (shadow property only in migration), never read or written.
- **ROOT CAUSE:** Missing entity property/configuration.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Domain/Entities/Queries.cs`, `src/SiteQueryDefectTracking.Infrastructure/Persistence/AppDbContext.cs`
- **STATUS:** OPEN

---

## BUG-018 — appsettings Api/Web base URLs and CORS origins mismatch actual dev URLs (LOW / P3)

- **MODULE:** Configuration
- **SEVERITY:** Low
- **PRIORITY:** P3
- **STEPS TO REPRODUCE:** Compare `Application:ApiBaseUrl`/`WebBaseUrl` and `Cors:Origins` with launch profiles.
- **EXPECTED RESULT:** Config matches actual URLs.
- **ACTUAL RESULT:** `https://localhost:5001`/`7102` and CORS origins do not match launch URLs (`5170`/`7146`, Web `5174`/`7181`).
- **ROOT CAUSE:** Stale values.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Api/appsettings.json:16-17,35`
- **STATUS:** OPEN

---

## BUG-019 — SendPreviewAsync accepts arbitrary recipient with no validation (MEDIUM / P2)

- **MODULE:** Email
- **SEVERITY:** Medium
- **PRIORITY:** P2
- **STEPS TO REPRODUCE:** `POST /api/email/preview` with any `to` value (e.g. attacker-controlled), no validation, logs created without query linkage.
- **EXPECTED RESULT:** Validate recipient; optionally restrict to manager's own email.
- **ACTUAL RESULT:** Arbitrary recipient accepted; unvalidated log rows.
- **ROOT CAUSE:** No `SendPreviewRequestValidator`.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/EmailService.cs:99-128`
- **STATUS:** OPEN

---

## BUG-020 — UserService.SearchAsync is case-sensitive & has no pagination bounds (LOW / P3)

- **MODULE:** Users
- **SEVERITY:** Low
- **PRIORITY:** P3
- **STEPS TO REPRODUCE:** Search `"manager"` matches; `"MANAGER"` may not depending on collation; pageSize unbounded (no max) unlike other searches.
- **EXPECTED RESULT:** Consistent case-insensitive search + capped page size.
- **ACTUAL RESULT:** `pageSize` unbounded; relies on DB collation.
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Infrastructure/Identity/UserService.cs:27-55`
- **STATUS:** OPEN

---

## BUG-021 — QueryNo generator can emit a duplicate sequence number (HIGH / P1)

- **MODULE:** Queries (POST /api/queries)
- **SEVERITY:** High
- **PRIORITY:** P1
- **STEPS TO REPRODUCE:**
  1. Query numbers follow `SQ-YYYYMM-####`.
  2. Create a query, then create another.
  3. Second create fails with `500` / `DbUpdateException`: "Cannot insert duplicate key row in object 'dbo.Queries' with unique index 'IX_Queries_QueryNo'."
- **EXPECTED RESULT:** Each new query gets a monotonically increasing, unique `QueryNo` (`...0001`, `...0002`, ...).
- **ACTUAL RESULT:** `GenerateQueryNoAsync` parsed the numeric tail starting at `prefix.Length`, i.e. it included the leading `-` (`"-0001"` parses to `-1`, `"-0000"` to `0`). `Max` was therefore bogus (e.g. max = `0`), so the generator kept returning the already-used `0001`.
- **ROOT CAUSE:** `QueryService.GenerateQueryNoAsync` used `v.Substring(prefix.Length)` instead of `v.Substring(prefix.Length + 1)` (skip the dash).
- **AFFECTED FILE:** `src/SiteQueryDefectTracking.Application/Services/QueryService.cs:358-373`
- **FIX:** Parse the tail from `prefix.Length + 1` (skip the `-`) with a guarded length check.
- **STATUS:** FIXED / VERIFIED (successive creates produce unique `SQ-202608-0002`, `-0003`, ...)

> Note: existing rows `SQ-202608-0000` and `SQ-202608-0001` were created by the buggy generator during QA. They are unique and do not block the fixed generator.

---

## BUG-022 — Login accepts different-case email (documented decision / NOT A BUG)

- **MODULE:** Auth (POST /api/auth/login)
- **OBSERVATION:** Login with `Manager@Demo.Local` (different case than seeded `manager@demo.local`) returns 200.
- **ANALYSIS:** ASP.NET Identity normalises emails/usernames to uppercase, so `FindByEmailAsync`/`FindByNameAsync` are intentionally case-insensitive. This is standard, accepted behaviour for email identifiers and does not weaken the password gate.
- **DECISION:** Accepted as designed; the login harness expectation was updated from 401 to 200 and no code change was made.
- **STATUS:** CLOSED (as designed)
