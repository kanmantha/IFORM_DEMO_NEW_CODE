# TEST STRATEGY

**Application:** SiteQueryDefectTracking (IFORM Site Query / Defect Tracking)
**Date:** 2026-08-09
**Environment:** Windows, .NET 10 SDK 10.0.302, SQL Server LocalDB (`MSSQLLocalDB`), API hosted on `http://localhost:5170` (Development)

---

## 1. Scope

| In scope | Out of scope / blocked |
|---|---|
| API functionality (all endpoints) | Blazor Web (template only, no features to test) |
| Authentication & authorization (roles, policies) | MAUI Mobile (template only) |
| Validation, negative & boundary cases | Docker build (Docker not installed on this machine) |
| Database schema & data integrity | SMTP delivery to a real mail server (SMTP disabled in dev) |
| Security (SQLi, XSS, IDOR, secrets, tampering) | Azure Blob storage (local provider used) |
| Concurrency & performance basics | Real 100k-row performance (LocalDB, seeded demo data) |
| Automated unit + integration tests | |

## 2. Test types

| Type | Approach |
|---|---|
| Functional | HTTP API calls per endpoint: happy path, missing fields, invalid data, duplicates, 404s |
| API | Verify HTTP status codes, JSON envelope, error structure consistency |
| Integration | xUnit tests against real API + LocalDB (WebApplicationFactory) |
| Database | Migration applied to fresh DB; verify tables/FKs/unique constraints; orphan prevention |
| Authentication | Login success/failure, disabled user, token refresh/rotation, logout |
| Authorization | Role-based: Manager vs Site Engineer vs Anonymous on each endpoint; IDOR attempts |
| Validation | Empty, whitespace, max lengths, negative/zero numbers, invalid enums, XSS/SQLi payloads |
| Security | SQLi strings, XSS payloads, path traversal, token tampering/expiry, secrets scan |
| Concurrency | Parallel status changes, duplicate refresh-token use, simultaneous creates |
| Performance | Response timing for search/snapshot/report with seeded data |
| Regression | Full suite re-run after each fix |

## 3. Test data

- Seeded demo users (see FEATURE_INVENTORY §5).
- Additional users created during tests are cleaned up or isolated by unique suffix per run.
- A dedicated QA database `SiteQueryDefectTrackingQA` on LocalDB (isolated from dev DB).

## 4. Bug lifecycle

1. Discover → 2. Reproduce (document exact request/response) → 3. Root cause (source) → 4. Fix → 5. Rebuild → 6. Re-test → 7. Record in `BUGS.md` → 8. Regression check.

Severity: Critical (blocks core flow / security), High, Medium, Low. Priority: P0–P3.

## 5. Exit criteria

- All builds succeed with 0 errors.
- No Critical/High unresolved bugs.
- Regression suite passes.
- Final report produced with production-readiness verdict.
