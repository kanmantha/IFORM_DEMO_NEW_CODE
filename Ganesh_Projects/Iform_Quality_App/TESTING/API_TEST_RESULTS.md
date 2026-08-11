# API TEST RESULTS

**Application:** SiteQueryDefectTracking (IFORM Site Query / Defect Tracking)
**Run date:** 2026-08-10
**Environment:** Windows 11, .NET 10 SDK 10.0.302, SQL Server **2025** LocalDB 17.0.1000.7 (`MSSQLLocalDB`), API on `http://localhost:5170` (Development), DB `SiteQueryDefectTrackingQA`.

> Environment note: during this QA cycle the machine's SQL Server 2025 instance was uninstalled (00:44), which also removed the original LocalDB runtime. It was restored by installing `SqlLocalDB.msi` **17.0.1000.7** (from the SQL Server 2025 Express media). All results below were captured AFTER restore, with the API running against LocalDB.

---

## 1. Suite summary

| # | Suite | Tooling | Cases | Result |
|---|---|---|---|---|
| 1 | Auth + API surface | `TESTING/scripts/run-api-tests.ps1` (Invoke-WebRequest) | 42 | **42 PASS / 0 FAIL** |
| 2 | Query lifecycle + IDOR | `TESTING/scripts/run-query-tests.ps1` | 19 | **19 PASS / 0 FAIL** |
| 3 | Unit tests | `dotnet test` — `tests/SiteQueryDefectTracking.UnitTests` | 69 | **69 PASS / 0 FAIL** |
| 4 | Integration tests | `dotnet test` — `tests/SiteQueryDefectTracking.IntegrationTests` (WebApplicationFactory + LocalDB) | 18 | **18 PASS / 0 FAIL** |
| | **Total** | | **148** | **148 PASS / 0 FAIL** |

---

## 2. Auth surface (`run-api-tests.ps1`, 18 of 42)

| Test | Method/Path | Expected | Actual |
|---|---|---|---|
| Login wrong password | POST /api/auth/login | 401 | 401 ✓ |
| Login unknown user | POST /api/auth/login | 401 | 401 ✓ |
| Login empty body | POST /api/auth/login | 400 (BUG-003) | 400 ✓ |
| Login missing password | POST /api/auth/login | 400 (BUG-003) | 400 ✓ |
| Login wrong-case email (`Manager@Demo.Local`) | POST /api/auth/login | 200 (as designed, BUG-022) | 200 ✓ |
| Login SQLi username (`' OR '1'='1`) | POST /api/auth/login | 401 | 401 ✓ |
| Login XSS username (`<script>…`) | POST /api/auth/login | 401 | 401 ✓ |
| Me — no token | GET /api/auth/me | 401 | 401 ✓ |
| Me — manager | GET /api/auth/me | 200 | 200 ✓ |
| Me — engineer | GET /api/auth/me | 200 | 200 ✓ |
| Logout | POST /api/auth/logout | 200 | 200 ✓ |
| Refresh with revoked token | POST /api/auth/refresh | 401 | 401 ✓ |
| Refresh valid (rotation) | POST /api/auth/refresh | 200 + new refresh token | 200 ✓ new token present |
| Refresh old token after rotation | POST /api/auth/refresh | 401 | 401 ✓ |
| Change password — wrong current | POST /api/auth/change-password | 400 | 400 ✓ |
| Change password — short new | POST /api/auth/change-password | 400 | 400 ✓ |
| Invalid token format (not a jwt) | GET /api/auth/me | 401 | 401 ✓ |
| Empty Bearer | GET /api/auth/me | 401 | 401 ✓ |

## 3. Queries (3 of 42)

| Test | Method/Path | Expected | Actual |
|---|---|---|---|
| Create query as manager | POST /api/queries | 200 (BUG-001 fixed) | 200 ✓ |
| Search queries — no auth | GET /api/queries | 401 | 401 ✓ |
| Search queries — engineer (mine only) | GET /api/queries | 200 | 200 ✓ |

## 4. Dashboard (3 of 42)

| Test | Method/Path | Expected | Actual |
|---|---|---|---|
| Dashboard snapshot — no auth | GET /api/dashboard/snapshot | 401 | 401 ✓ |
| Dashboard snapshot — manager | GET /api/dashboard/snapshot | 200 | 200 ✓ |
| Dashboard open — engineer | GET /api/dashboard/open | 403 (BUG-004) | 403 ✓ |

## 5. Authorization — engineer attempting manager actions (8 of 42)

| Test | Method/Path | Expected | Actual |
|---|---|---|---|
| List users | GET /api/users | 403 | 403 ✓ |
| Create user | POST /api/users | 403 | 403 ✓ |
| Change query status | PUT /api/queries/{id}/status | 403 | 403 ✓ |
| Resolve query | PUT /api/queries/{id}/resolve | 403 | 403 ✓ |
| Create product | POST /api/products | 403 | 403 ✓ |
| Email templates | GET /api/email/templates | 403 | 403 ✓ |
| Audit log | GET /api/audit | 403 | 403 ✓ |
| Dashboard (no auth) | GET /api/dashboard/snapshot | 401 | 401 ✓ |

## 6. Products (4 of 42)

| Test | Method/Path | Expected | Actual |
|---|---|---|---|
| Search products (manager) | GET /api/products | 200 | 200 ✓ |
| Get product — unknown id | GET /api/products/00000000-… | 404 | 404 ✓ |
| Get product detail | GET /api/products/{id} | 200 | 200 ✓ |
| Create duplicate product code | POST /api/products | 400 | 400 ✓ |

## 7. Email / Audit / Notifications / Reports / Health (6 of 42)

| Test | Method/Path | Expected | Actual |
|---|---|---|---|
| List email templates | GET /api/email/templates | 200 | 200 ✓ |
| Audit log search (manager) | GET /api/audit?pageSize=5 | 200 | 200 ✓ |
| Notifications (mine) | GET /api/notifications | 200 | 200 ✓ |
| Report — OpenQueries CSV | POST /api/reports/generate | 200 + text/csv | 200 ✓ |
| Report — invalid type (99) | POST /api/reports/generate | 400 (BUG-003) | 400 ✓ |
| Report — reversed date range | POST /api/reports/generate | 400 (BUG-003) | 400 ✓ |
| Health | GET /health | 200 | 200 ✓ |

---

## 7. Query lifecycle & IDOR (`run-query-tests.ps1`)

All 19 cases PASS. Highlights:

- **Create query** — missing IPO → 400; missing project → 400; unknown project → 404; negative quantity → 400; valid → 200 with new id.
- **Read/update** — detail 200; nonexistent 404; update open 200; update after resolve 400.
- **Comments** — add 200; list 200; empty text 400; comment on resolved 400 (rejected).
- **Status lifecycle** — Pending→InProgress 200; InProgress→Pending 200; same status 400; resolve 200; re-resolve 400.
- **Resolve history (BUG-002)** — history now records `fromStatus=Pending -> toStatus=Resolved` (previously `Resolved -> Resolved`).
- **QueryNo uniqueness (BUG-021)** — successive creates produced unique `SQ-YYYYMM-0001…0003` (previously duplicates caused 500 / unique-index violation).
- **IDOR (BUG-005)** — engineer1 GET/comment/update on engineer2's query → **403** (previously 200).

---

## 8. Unit tests (69)

| Area | Tests |
|---|---|
| Validators | CreateQuery, Comment, Login, ChangePassword, Report, Product, CreateUser, ResetPassword, EmailTemplate, SendEmail, ProductSearch |
| Domain | DelaySeverityClassifier, QueryMappers, AuditLogMapper |
| Infrastructure | TokenService, SystemClock, DelayCalculator |

Result: `Failed: 0, Passed: 69, Skipped: 0, Total: 69`.

## 9. Integration tests (18)

WebApplicationFactory boots the real API against LocalDB `SiteQueryDefectTrackingQA`. Two issues were fixed in the suite itself:

1. `GetLookupsAsync` used an **unauthenticated** client for `/api/projects/active` + `/api/queries` — after BUG-004 these require auth; helper now uses an authenticated client.
2. `Report_ValidOpenCsv_Returns200Csv` asserted the HTTP Content-Type `text/csv`, but reports are returned as a JSON envelope with `data.contentType` (base64 body); assertion now reads `data.contentType` and verifies the decoded CSV starts with `Query No`.
3. Parallelization disabled (`xunit.runner.json`, `parallelizeTestCollections=false`, `parallelizeAssembly=false`) because the assembly targets a shared real LocalDB — two bootstrapped WebApplicationFactories racing migrations/Identity produced transient 500s on login.

Result: `Failed: 0, Passed: 18, Skipped: 0, Total: 18`.

Covered: login/me unauthenticated + tampered token, create query (manager), create query missing IPO → 400, query auth health (401 anonymous), IDOR read+comment → 403, engineer dashboard → 403, engineer create product → 403, resolve history `Pending -> Resolved` + post-resolve comment 400, report invalid type 400, reversed range 400, valid OpenQueries CSV 200.

---

## 10. Observations / residual risk

- **Transient 500s under parallel WebApplicationFactory runs** were traced to DB contention with two in-process hosts seeding/migrating the same LocalDB; resolved by disabling collection parallelization. Not an app defect.
- **LocalDB environment fragility**: this machine's SQL Server 2025 install (and its bundled LocalDB) was uninstalled mid-QA; tests only passed again after reinstalling LocalDB 17. See `FINAL_TEST_REPORT.md` for implications.
- No failures remain that indicate application regressions. All BUG-001…005, BUG-021 behaviours verified against the running API and automated suites.