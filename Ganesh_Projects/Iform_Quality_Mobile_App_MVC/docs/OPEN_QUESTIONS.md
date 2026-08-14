# Open Questions & Decisions — I-FORM Site Query

## BRD open questions (Section 9) — still open

| # | Question | Current behaviour / placeholder | Recommended default |
|---|---|---|---|
| 1 | Should an unresolved query escalate beyond the Manager after N days? | Implemented: background rule escalates (severity → Critical + `CriticalDelay` notification to the configured role) when `delay ≥ Days`. Config: `Features:Escalation:Enabled=true`, `Days=10`, `Role=TenantAdmin` (overridable per tenant via `TenantSettings`). Runs every 6h via `MaintenanceBackgroundService`; idempotent per query. | Decide final escalation audience (currently TenantAdmin by default) and whether escalation should also be visible to the Manager. |
| 2 | Who owns uploading/maintaining the Module 4 catalogue? | `Features:Catalogue:Owner=TenantAdmin`; seed + import actions are TenantAdmin-only (`ProductsController`). | Keep TenantAdmin as owner; document the onboarding/refresh SLA (BRD: searchable within 24h of upload — currently immediate). |
| 3 | Email template variants beyond the 4 issue types? | One template per issue type; templates are editable, project-agnostic. | Keep project-agnostic for now; add per-project override only if requested. |
| 4 | Photo retention period? | Implemented: `Features:Photos:RetentionMonths` (default `0` = keep forever). When > 0, the 6-hourly maintenance job purges `QueryPhotos` older than the cutoff and deletes the underlying stored files. | Set the production value (e.g., 24 months after Resolve) once agreed. |

## Product / roadmap questions

- **Build vs buy**: BRD §11.4 flags that mature commercial tools (Planfred, BuildPass, etc.) cover much of Phase 1+2. Decision on whether to continue the custom SaaS build beyond pilot must be explicit.
- **Pilot**: BRD §8.2 says pilot on 2–3 projects with the Excel tracker run in parallel. Seeded demo data (1 project, 1 query, 1 EOT) is test data only.

## Architecture decisions made this session (worth confirming)

- **Multi-tenant model**: shared-schema with a `TenantId` filter (no row-level security). If tenants need hard isolation guarantees, move to SQL RLS or per-tenant databases in production.
- **Super Admin visibility**: superadmin bypasses the tenant filter to see all tenants/users (usage, reports). Confirmed desired; keep as-is.
- **`ApplicationUser` not tenant-filtered**: Identity table shared; user scoping enforced in `UserManagementService`. If a tenant admin must never enumerate other tenants' users, this is already satisfied.
- **Platform tenant**: represented as a real `Tenants` row with `Id = Guid.Empty` to satisfy the `AspNetUsers.Tenants_TenantId` FK. Confirm this reserved-id approach for production.
- **File access control**: `FilesController.Download` now only streams paths referenced by tenant-owned records (QueryPhotos/EotDocuments/Products). Storage keys are random GUIDs. Good for now; revisit with signed URLs if storage moves to a public CDN.
- **Email provider**: `Log` by default; SMTP config present. Need real SMTP/relay credentials before pilot.
- **Payment**: mock provider only; real gateway integration deferred.
- **Mobile approach**: PWA, not a native app. NFR-1 (iOS+Android) is satisfied; confirm QR-code install path (FR-1.1) and that offline capture (Phase 2) is acceptable deferred.

## Known gaps / follow-ups (from verification)

- **Tests**: 38 real tests green — `tests/IForm.UnitTests` (22: query business rules + accessory catalogue uniqueness), `tests/IForm.SecurityTests` (9: tenant isolation + file download authorization against SQLite), `tests/IForm.IntegrationTests` (7: `DbSeeder` clean/idempotent/self-healing + `MaintenanceServicesTests` covering query escalation (notifies configured role once, idempotent, threshold respected) and photo retention (purges expired photos + deletes files) against in-memory SQLite, with `Foreign Keys=False` where FKs reference unmapped users and `IgnoreQueryFilters()` where the tenant filter hides seeded rows).
- **EOT approvals**: verified end-to-end via HTTP — 7 required document uploads, Submit, full transition chain (Submitted → UnderReview → ClientSignoffPending → ContractsReview → Approved), status badge `Approved`, and in-app `EotApproved` notification. Note: approval creates an in-app notification only; automatic email-on-approval is NOT implemented (emails are manual via the Compose/Send flow) — confirm whether an auto-email rule is wanted.
- **Report verification**: all 5 reports exercised with populated data (7 queries, 2 projects, 3 IPOs, 2 EOTs, 2 engineers). DelayReport shows severity variety (50d Severe / 35d Critical / 20d Delayed / 10d Watch / 3d Normal); ProductIssueReport groups issues by linked product; EngineerReport aggregates per engineer; Excel export verified for all 5 report types (valid xlsx).
- **Bug fixed while verifying**: `QueryService.CreateAsync` set `ProductId = request.ProductId` directly, but the mobile/desktop create form only captures `ProductCode` — so `ProductId` was always null and the Product Issue report was permanently empty. Now resolves `ProductId` from the tenant's catalogue by code when the request doesn't supply it. (Existing rows were backfilled via SQL; new queries auto-link.)
- **ImageSharp**: photo resize uses the MIT-licensed `SixLabors.ImageSharp` 3.1.12. Version 4.x requires a paid license at build time and would break CI; stay on 3.x unless a license key is purchased (`SixLaborsLicenseKey`).
- **Docker/CI-CD**: added `Dockerfile` (multi-stage, `mcr.microsoft.com/dotnet/aspnet:10.0`), `docker-compose.yml` (web + SQL Server 2022 with health gate; env-overridable SQL password and superadmin seed), and `.github/workflows/ci.yml` (restore → build Release → test → publish artifacts). Verified locally: build, 34/34 tests, and publish all pass.
- **Demo data**: demo tenant has 2 projects (PRJ-1002, PRJ-1003), 3 IPOs (IPO-3001/3002/4001), 7 queries (SQ-0001..0007 with backdated `RaisedDate` for severity variety), 2 EOTs (EOT-01 Approved, EOT-02 Draft), and 2 engineers. Repeatable via `scripts/seed-demo-data.ps1` (idempotent; optional `-SqlConnectionString` backdates raised dates for the delay report).
- **Maintenance jobs**: `MaintenanceBackgroundService` (registered in `Program.cs`, 6h interval, 2-min initial delay) runs both jobs via DI — `IEscalationService.ProcessEscalationsAsync` and `IPhotoRetentionService.PurgeExpiredAsync`. Escalation verified live: first run escalated SQ-0003/0004/0005/0006 (11/21/36/51 days) and the `CriticalDelay` notifications appeared on the TenantAdmin's notification list. Photo retention kept off by default (`RetentionMonths=0`); logic covered by integration test.
