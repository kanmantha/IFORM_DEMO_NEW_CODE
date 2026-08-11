# FEATURE INVENTORY

**Application:** SiteQueryDefectTracking (IFORM Site Query / Defect Tracking)
**Repository root:** `G:\Ganesh_Projects\Iform_Quality_App`
**Inventory date:** 2026-08-09 (updated 2026-08-10 after fixes + regression run)
**Build:** net10.0, EF Core 10 (SQL Server), ASP.NET Core Identity + JWT Bearer, SignalR, FluentValidation, ClosedXML, MailKit.

> Legend — Test status: `NOT TESTED` / `PASS` / `FAIL` / `BLOCKED`. Updated as testing proceeds.

---

## 1. Solution structure

| Project | Role | Notes |
|---|---|---|
| `src/SiteQueryDefectTracking.Domain` | Domain layer | Entities, enums, events, contracts, constants |
| `src/SiteQueryDefectTracking.Application` | Application layer | Services, DTOs, validators, pagination, exceptions |
| `src/SiteQueryDefectTracking.Infrastructure` | Infrastructure layer | EF Core, Identity, JWT tokens, SMTP, file storage, migrations |
| `src/SiteQueryDefectTracking.Api` | ASP.NET Core Web API | Controllers, JWT auth, SignalR hub, middleware, OpenAPI |
| `src/SiteQueryDefectTracking.Web` | Blazor Web template | **Not implemented — template only** |
| `src/SiteQueryDefectTracking.Mobile` | .NET MAUI template | **Not implemented — template only** |
| `tests/SiteQueryDefectTracking.UnitTests` | xUnit | **69 tests PASS** (validators, domain classifiers, mappers, token service, clock, delay calc) |
| `tests/SiteQueryDefectTracking.IntegrationTests` | xUnit | **18 tests PASS** (WebApplicationFactory + LocalDB QA DB) |
| `tools/DatabaseSeeder` | Console | Seeds DB using DbSeeder |
| `tools/ProductCatalogueImporter` | Console | CSV/XLSX product import |

---

## 2. Modules and features

### Module A — Authentication & Identity (`api/auth`, `api/users`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| A1 | Login (username or email) | POST `/api/auth/login` | Anonymous | Returns access + refresh token | `AuthService.LoginAsync`; logs LoginFailed on failure; blocks disabled users | PASS (wrong pw 401, unknown 401, empty body/missing pw 400, case-insensitive email 200) |
| A2 | Refresh token | POST `/api/auth/refresh` | Anonymous | Rotates refresh token, returns new pair | `AuthService.RefreshAsync`; hashes token; revokes old | PASS (valid 200 + rotation; revoked + old-token-after-rotation 401) |
| A3 | Logout | POST `/api/auth/logout` | Authenticated | Revokes refresh token | `AuthService.LogoutAsync` | PASS (200; refreshed token then 401) |
| A4 | Change password | POST `/api/auth/change-password` | Authenticated | Validates current + new password | `AuthService.ChangePasswordAsync` | PASS (wrong current 400, short new 400) |
| A5 | Current user (`me`) | GET `/api/auth/me` | Authenticated | Returns user + roles | `UserService.GetCurrentUserAsync` | PASS (no token 401, manager/engineer 200) |
| A6 | List/search users | GET `/api/users` | Manager | Search users by keyword/role, paged | `UserService.SearchAsync` (role filter accepted but not applied — bug) | PASS (manager 200; engineer 403) |
| A7 | Create user | POST `/api/users` | Manager | Create user, assign roles | `UserService.CreateAsync` | PASS (engineer 403) |
| A8 | Update user | PUT `/api/users/{id}` | Manager | Update name/phone/active/roles | `UserService.UpdateAsync` | NOT TESTED (role-gated; manager-only path not exercised) |
| A9 | Reset password | POST `/api/users/{id}/reset-password` | Manager | Force password reset | `UserService.ResetPasswordAsync` | NOT TESTED |

### Module B — Queries (`api/queries`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| B1 | Create query | POST `/api/queries` | Any authenticated | Validates, generates QueryNo, sets status Pending, raises audit + event | `QueryService.CreateAsync` — **auth check inverted (bug)** | PASS (BUG-001 fixed; manager 200; missing IPO 400, missing project 400, unknown project 404, negative qty 400, unique QueryNo BUG-021) |
| B2 | Get query detail | GET `/api/queries/{id}` | Authenticated (owner or manager) | Returns detail with comments/history/attachments/emails | `QueryService.GetAsync` + `EnforceAccess` | PASS (200; nonexistent 404; cross-engineer 403) |
| B3 | Search queries | GET `/api/queries` | Authenticated | Filter/sort/page; scoped for engineers | `QueryService.SearchAsync` (mineOnly bypass — bug) | PASS (no auth 401; engineer scoped 200; engineer sees own only - BUG-004) |
| B4 | Update query | PUT `/api/queries/{id}` | Owner/manager | Edit open query fields | `QueryService.UpdateAsync` | PASS (200; resolved-edit 400; cross-engineer 403) |
| B5 | Change status | PUT `/api/queries/{id}/status` | Manager | Valid transition Pending→InProgress→Resolved; history entry | `QueryService.ChangeStatusAsync` | PASS (Pending→InProgress 200; InProgress→Pending 200; same status 400; engineer 403) |
| B6 | Resolve query | PUT `/api/queries/{id}/resolve` | Manager | Sets Resolved + ResolvedBy + history | `QueryService.ResolveAsync` — **FromStatus bug** | PASS (BUG-002 fixed; history `Pending -> Resolved`; already-resolved 400; engineer 403) |
| B7 | Add comment | POST `/api/queries/{id}/comments` | Authenticated | Comment on open query; audit + event | `QueryService.AddCommentAsync` (no access enforcement — bug) | PASS (BUG-005 fixed; 200; empty text 400; resolved-query 400; cross-engineer 403) |
| B8 | Get comments | GET `/api/queries/{id}/comments` | Authenticated | List comments for query | `QueryService.GetCommentsAsync` (no access enforcement — bug) | PASS (BUG-005 fixed; list 200; cross-engineer 403) |

### Module C — Dashboard (`api/dashboard`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| C1 | Dashboard snapshot | GET `/api/dashboard/snapshot` | Authenticated (CanViewDashboard) | Counts, delay buckets, breakdowns | `DashboardService.GetSnapshotAsync` | PASS (no auth 401; manager 200; engineer 403 after BUG-004) |
| C2 | Open queries list | GET `/api/dashboard/open` | Authenticated | All open queries, most delayed first | `DashboardService.GetOpenQueriesAsync` — **exposes all queries to engineers (bug)** | PASS (engineer 403 after BUG-004) |

### Module D — Product catalogue (`api/products`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| D1 | Search products | GET `/api/products` | Authenticated | Filter by query/category/project, paged | `ProductService.SearchAsync` | PASS (manager 200) |
| D2 | Get product | GET `/api/products/{id}` | Authenticated | Detail + specs + mappings | `ProductService.GetAsync` | PASS (200; unknown id 404) |
| D3 | Create product | POST `/api/products` | Manager | Duplicate code check; audit | `ProductService.CreateAsync` (Name=Description — bug) | PASS (duplicate code 400; engineer 403) |
| D4 | Update product | PUT `/api/products/{id}` | Manager | Replace specs/mappings | `ProductService.UpdateAsync` | NOT TESTED |
| D5 | Import preview | POST `/api/products/import/preview` | Manager | Validate rows, return job id | `ProductService.ImportPreviewAsync` (in-memory job store) | NOT TESTED |
| D6 | Import commit | POST `/api/products/import/{jobId}/commit` | Manager | Upsert products | `ProductService.CommitImportAsync` | NOT TESTED |
| D7 | Import status | GET `/api/products/import/{jobId}/status` | Manager | Job status | `ProductService.GetImportStatusAsync` | NOT TESTED |

### Module E — Email (`api/email`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| E1 | List templates | GET `/api/email/templates` | Manager | List email templates | `EmailTemplateService.GetAllAsync` | PASS (manager 200; engineer 403) |
| E2 | Upsert template | POST `/api/email/templates` | Manager | Create/update template by code | `EmailTemplateService.UpsertAsync` | NOT TESTED |
| E3 | Generate email | POST `/api/email/generate` | Manager | Render template → draft EmailLog | `EmailService.GenerateAsync` | NOT TESTED |
| E4 | Send email | POST `/api/email/send` | Manager | Send via SMTP, log result | `EmailService.SendAsync` | NOT TESTED (SMTP disabled in dev) |
| E5 | Preview email | POST `/api/email/preview` | Manager | Send arbitrary preview | `EmailService.SendPreviewAsync` (no validation — bug) | NOT TESTED |

### Module F — Notifications (`api/notifications`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| F1 | My notifications | GET `/api/notifications` | Authenticated | Own notifications | `NotificationService.GetMineAsync` | PASS (manager 200; returns empty as F3 not wired) |
| F2 | Mark read | POST `/api/notifications/read` | Authenticated | Mark own as read | `NotificationService.MarkReadAsync` | NOT TESTED |
| F3 | (Generate notifications) | internal | — | Domain events create notifications | `NotificationWriter` **not wired to events — no notifications ever created (bug)** | BLOCKED (BUG-014 open) |

### Module G — Audit (`api/audit`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| G1 | Search audit log | GET `/api/audit` | Manager | Filter by action/entity/user/date, paged | `AuditLogService.SearchAsync` | PASS (manager 200; engineer 403) |
| G2 | Record audit entries | internal | — | All services record actions | `AuditLogService.RecordAsync` — **Username always null (bug)** | PASS (audit rows exist) — Username bug BUG-007 OPEN |

### Module H — Projects (`api/projects`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| H1 | Active projects | GET `/api/projects/active` | Authenticated | List active lookup items | `ProjectService.GetActiveAsync` | PASS (authenticated 200; used by query harness) |
| H2 | Search projects | GET `/api/projects` | Authenticated | Paged project lookup | `ProjectService.SearchAsync` | PASS (used by harness lookups) |

### Module I — Reports (`api/reports`)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| I1 | Generate report | POST `/api/reports/generate` | Authenticated (CanViewDashboard) | Excel/CSV/HTML export | `ReportService.GenerateAsync` (PDF returns HTML — bug) | PASS (CSV 200 + content-type text/csv; invalid type 400; reversed date range 400 — BUG-003; engineer 403) |

### Module J — Realtime (SignalR)

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| J1 | Query events hub | `/hubs/queries` | Authenticated | Publish create/update/status/comment events | `QueriesHub` — **not `RequireAuthorization()`; broadcasts to all clients (bug)** | BLOCKED (BUG-008 open; SignalR client harness not automated) |

### Module K — Files/attachments

| # | Feature | Screen/API | Role | Expected behavior | Current implementation | Test status |
|---|---|---|---|---|---|---|
| K1 | Upload attachment | — (no endpoint) | Authenticated | Store + link to query | **No upload endpoint exists**; `AttachmentDto` download URLs point to `/api/queries/{id}/attachments/{id}/download` which **does not exist (dead link)** | BLOCKED (BUG-015 open) |
| K2 | Download attachment | — (no endpoint) | Authenticated | Serve stored file | **No download endpoint exists** | BLOCKED (BUG-015 open) |

---

## 3. Cross-cutting concerns

| Concern | Status | Notes |
|---|---|---|
| Global error handling | Present | `ExceptionHandlingMiddleware`: Unauthorized→401, Forbidden→403, NotFound→404, Conflict→409, Validation→400, Business→400, other→500 |
| Response envelope | Present | `{ success, data, message }` via `ApiResponse` |
| Pagination | Present | `PagedResult<T>`; default page 1/pageSize 25, max 100 |
| Validation | Present | **BUG-003 fixed**: `FluentValidationActionFilter` globally applied to all action arguments; login/create-query/report validators verified via harness |
| Authorization policies | Present | Manager-only: users, resolve/status, catalogue, email, audit. **Dashboard tightened to `RequireRole(Manager)` (BUG-004)**; Projects controller plain `[Authorize]` |
| CORS | Present | `AllowFrontend` policy; origins from config; wildcard+credentials guard verified at startup |
| Health check | Present | GET `/health` — 200 |
| OpenAPI | Present | `/openapi/v1.json` (Development only) — security scheme not documented |
| Auto-migration + seed | Present | Runs on API startup for **all environments**; seed users use configurable default password |
| File storage | Present | Local (`assets/uploads`) / Azure Blob switch; `Storage:LocalRoot` config key mismatch (options bind `RootPath`) |
| SMTP | Present | Config key mismatch: `SmtpHost`/`SmtpPort` in appsettings vs `Host`/`Port` in `EmailOptions` (bug) |

---

## 4. Roles matrix

| Role | Auth | Queries CRUD | Status/Resolve | Users | Catalogue | Email | Audit | Dashboard/Reports |
|---|---|---|---|---|---|---|---|---|
| Manager | Yes | All | Yes | Yes | Yes | Yes | Yes | Yes |
| Site Engineer | Yes | Own only (enforced — BUG-004 fixed) | No | No | No | No | No | No |
| Anonymous | Login/Refresh only | No | No | No | No | No | No | No |

---

## 5. Seed/demo data

| Item | Value |
|---|---|
| Roles | `Manager`, `Site Engineer` |
| Users | `manager@demo.local` (Demo Manager, Manager), `siteengineer@demo.local` (Site Engineer, Site Engineer), `engineer2@demo.local` (Ravi Kumar, Site Engineer) |
| Password (default) | `Demo@1234!` (config `Seed:DefaultPassword`) |
| Issue types | MISSING, PRODUCTION_MISTAKE, DESIGN_MISTAKE, DISPATCH_MISSING |
| Projects | PRJ-CHN-001..PRJ-CBE-001 (5) |
| Products | 32 codes (DAAA..DTAA0010) + project mappings |
| Queries | SQ-0001..SQ-0010 with status history + comments |
| Email templates | 4 defaults, code = issue type code |

---

## 6. Test coverage summary (2026-08-10)

| Suite | Cases | Result |
|---|---|---|
| `run-api-tests.ps1` (HTTP harness on running API) | 42 | **42/42 PASS** |
| `run-query-tests.ps1` (query lifecycle + IDOR) | 19 | **19/19 PASS** |
| Unit tests (validators, domain, infrastructure) | 69 | **69/69 PASS** |
| Integration tests (WebApplicationFactory + LocalDB) | 18 | **18/18 PASS** |

**Verdict:** All automated suites green. Bugs fixed & verified: BUG-001, BUG-002, BUG-003, BUG-004, BUG-005, BUG-021. Open (see `BUGS.md`): BUG-006..BUG-020 (mostly Medium/Low config and unimplemented-feature defects). See `API_TEST_RESULTS.md` for full detail and `FINAL_TEST_REPORT.md` for the production-readiness verdict.
