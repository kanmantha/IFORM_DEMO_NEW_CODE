# FINAL TEST REPORT

**Application:** SiteQueryDefectTracking (IFORM Site Query / Defect Tracking)
**Date:** 2026-08-10
**Build:** net10.0, ASP.NET Core 10, EF Core 10 (SQL Server), FluentValidation 12, ClosedXML, CsvHelper, SignalR, Identity + JWT.
**Result:** 🟢 **CONDITIONAL PASS — NOT production-ready out of the box** (release-blocking items listed below).

---

## 1. Executive summary

An end-to-end QA cycle was completed for the **API** (the Web and Mobile projects are template-only). Six defects were found and fixed with regression coverage, and all four automated suites now pass:

| Suite | Cases | Result |
|---|---|---|
| HTTP API harness (`run-api-tests.ps1`) | 42 | 42/42 PASS |
| Query lifecycle & IDOR harness (`run-query-tests.ps1`) | 19 | 19/19 PASS |
| Unit tests | 69 | 69/69 PASS |
| Integration tests (WebApplicationFactory + LocalDB) | 18 | 18/18 PASS |
| **Total** | **148** | **148/148 PASS** |

The core domain flows (auth, query lifecycle, status/resolve with history, comments, validation, role authorization, IDOR protection, product catalogue reads, reports, dashboard gating) are functionally correct and verified. The product is **not yet production-ready** because of unresolved Medium-severity configuration/security defects (secrets in `appsettings.json`, SMTP misconfiguration, dead email-preview validation, anonymous SignalR hub), plus a dependency/DevOps concern (Docker + UI not testable in this environment). Details and a clear go/no-go gate are in §7.

---

## 2. Scope & environment

**In scope:** API functional, authentication/authorization, validation, security (SQLi/XSS/IDOR/token tampering), database integrity, reports, automation.

**Out of scope / blocked:**
- Blazor Web & MAUI Mobile — `src/*` are template-only, no features to test.
- Docker deployment pipeline — Docker not installed on the machine.
- SMTP live delivery — SMTP not configured in dev.
- Azure Blob storage — local provider used.
- Large-scale (100k-row) performance — LocalDB with seeded demo data only.

**Environment incident during QA:** the machine's SQL Server 2025 engine and its bundled LocalDB runtime were uninstalled mid-cycle (uninstall log `20260810_004410`). LocalDB 17.0.1000.7 (2025) was reinstalled from the SQL Server 2025 Express media (`SQL2025-SSEI-Expr.exe /ACTION=Download /MEDIATYPE=LocalDB` → `SqlLocalDB.msi`, installed elevated). After restore the API returned to healthy and every suite passed. **This must be documented as a reproducibility risk for the QA environment (see §7).**

---

## 3. What was fixed & verified

| BUG | Severity | Fix | Verification |
|---|---|---|---|
| BUG-001 Create query always 401 | Critical | Removed inverted auth guard in `QueryService.CreateAsync` | Create query 200 (manager + engineers) |
| BUG-002 Resolve history `FromStatus` wrong | High | Capture previous status before mutation | history shows `Pending -> Resolved` |
| BUG-003 Validators never executed | High | Global `FluentValidationActionFilter` registered in MVC options | login/empty, report type 99, reversed date range → 400 |
| BUG-004 Engineers could see all queries | High | Dashboard policy → `RequireRole(Manager)`; `ProjectsController` → `[Authorize]`; `SearchAsync` always scopes non-managers to `RaisedByUserId` | engineer dashboard/open → 403 |
| BUG-005 Comment IDOR | High | `EnforceAccess` added to `AddCommentAsync`/`GetCommentsAsync` | cross-engineer GET/comment → 403 |
| BUG-021 QueryNo duplicate sequence | High | QueryNo tail parsed skipping the `-` (`prefix.Length + 1`) | successive creates → unique `SQ-202608-0002…0003` |

A dedicated integration test confirms the IDOR + resolve-history + dashboard + report-validation behaviours in code, so they are regression-protected.

---

## 4. Open defects (see `BUGS.md` for detail)

**Medium — should be resolved before deploy:**
- BUG-006 SMTP config keys mismatch (`SmtpHost` vs `Host`) → emails never actually sent.
- BUG-007 Audit logs never store username.
- BUG-008 SignalR hub not `RequireAuthorization()`; broadcasts to all clients.
- BUG-009 Secrets (SA password, JWT key, seed password) committed in `appsettings.json`.
- BUG-011 "PDF" report returns HTML named `*.pdf.html`.
- BUG-014 `NotificationWriter` not wired to domain events → in-app notifications never created.
- BUG-015 Attachment upload/download endpoints don't exist; DTO exposes dead download URLs.
- BUG-019 `SendPreviewAsync` accepts arbitrary recipient with no validation.

**Low (tracked, not release-blocking):** BUG-010 decimal precision warning, BUG-012 role filter ignored on user search, BUG-013 product name=description, BUG-016 storage key mismatch, BUG-017 Slab dead, BUG-018 stale base URLs/CORS, BUG-020 case-sensitive/unbounded user search.

---

## 5. Test coverage summary

| Feature area (# FEATURE_INVENTORY) | Result |
|---|---|
| Authentication (login/refresh/logout/me/change-password, tampering, SQLi/XSS) | PASS |
| Queries (create/read/update/status/resolve/comments/history/QueryNo) | PASS |
| Dashboard gating | PASS |
| Authorization / role matrix (Manager vs Engineer vs Anonymous) | PASS |
| IDOR on query read/update/comment | PASS |
| Products (search/detail/duplicate) | PASS |
| Email (templates list) | PASS — send/preview BLOCKED (BUG-006/019) |
| Audit (list) | PASS — username column bug (BUG-007) |
| Notifications (list) | PASS — generation dead (BUG-014) |
| Reports (CSV valid, invalid type, reversed range) | PASS |
| Realtime (SignalR) | BLOCKED (BUG-008; no automated client harness) |
| Attachments | BLOCKED (BUG-015; no endpoints) |
| Web / Mobile UI | BLOCKED (template-only) |

---

## 6. Automation assets delivered

- `tests/SiteQueryDefectTracking.UnitTests` — 69 tests (validators, DelaySeverityClassifier, mappers, TokenService, SystemClock, DelayCalculator).
- `tests/SiteQueryDefectTracking.IntegrationTests` — 18 tests using `WebApplicationFactory<Program>` against LocalDB; `xunit.runner.json` disables parallelization (shared real DB); covers auth, queries, IDOR, dashboard, resolve-history, reports.
- `TESTING/scripts/run-api-tests.ps1`, `run-query-tests.ps1` — black-box HTTP harnesses (42 + 19 cases) against the running API.
- Documentation: `FEATURE_INVENTORY.md`, `TEST_STRATEGY.md`, `BUGS.md`, `API_TEST_RESULTS.md`, this report.

---

## 7. Production-readiness verdict

**CONDITIONAL PASS.** The application is functionally sound on its core paths and all automated tests are green, but it requires the following before a live deployment:

### Go / no-go gate
**NO-GO for production as-is.** Blockers:

1. **Secrets in source control (BUG-009).** SA password, JWT signing key and seed password are committed in `appsettings.json`. Must move to environment variables / user-secrets / a secrets manager, rotate all exposed credentials.
2. **Email is non-functional in prod config (BUG-006).** SMTP options never bind → no real email can be sent; either fix the config-key mapping (or bind block) and verify with a real SMTP server, or confirm email is out of scope for v1.
3. **Attachment feature is absent but exposed (BUG-015).** Query DTO advertises download URLs that 404. Either implement upload/download endpoints or stop exposing the dead links.
4. **SignalR hub is anonymous and emits to all clients (BUG-008).** Security gap for realtime updates; add `RequireAuthorization()` and user-scoped delivery.
5. **Committed seed behaviour for all environments.** Auto-migrate + seed runs on every startup in every environment (`Program.cs`) — dangerous on production when pointed at the real DB; scope DB creation/bootstrap to Development.

### Recommended before release (medium)
- BUG-007 audit username, BUG-011 misleading PDF/HTML report name, BUG-014 notifications wiring, BUG-019 preview validation, Docker build + CI wiring, Web/Mobile feature implementation and UI test pass.

### Environment reproducibility note
The QA box lost its SQL Server engine mid-cycle; this QA run was completed on LocalDB 17.0.1000.7. Any deployment/CI must not assume LocalDB: provision a real SQL Server (or container) with migration-friendly startup, and pin the SQL Server version in the build pipeline.

---

## 8. Sign-off

| Area | Status |
|---|---|
| Build (all 12 projects) | ✅ |
| Unit tests | ✅ 69/69 |
| Integration tests | ✅ 18/18 |
| HTTP API regression | ✅ 42 + 19 |
| Security checks (SQLi, XSS, IDOR, token tampering) | ✅ |
| DB integrity + unique constraints | ✅ |
| Production readiness | ⚠️ CONDITIONAL (items in §7) |