# Requirements Traceability — Site Query & Defect Tracking App

Source: `G:\Ganesh_Projects\IForm_MobileApp\docs\Site Query App - BRD.txt` (v1.0, Aug 2026).

Status legend: ✅ Implemented & verified at runtime · 🔶 Implemented, partially verified · ⏳ Deferred (per BRD roadmap).

## Module 1 — Report

| ID | Requirement | Implementation | Status |
|---|---|---|---|
| FR-1.1 | Install via QR code scan | PWA (`manifest.json`, `sw.js`, install prompt). QR points to the PWA URL. | 🔶 |
| FR-1.2 | Issue types: Missing, Production Mistake, Design Mistake, Dispatch Missing | `IForm.Domain/Enums/Enums.cs` (`enum IssueType` = 1..4), `QueriesController.Create` + `Views/Queries/Create.cshtml`. | ✅ |
| FR-1.3 | Attach photo, qty (nos & sqm), project, IPO | `CreateQueryRequest` (ProductCode/Name, QuantityNos, QuantitySqm, ProjectId, IpoId, Photos), `QueryService.CreateAsync`. Photos normalized (ImageSharp) and stored. | ✅ |
| FR-1.4 | Status auto-progresses Pending → In Progress → Resolved | `QueryStatus` enum; status transitions in `QueryService` (`StartWork`, `Resolve`); `QueryStatusHistory` audit. | ✅ |
| FR-1.5 | Delay days auto-calculated from raise date | `QueryService` `delay = (today − RaisedDate)`; `QueryBusinessRules.ClassifySeverity`. | ✅ |
| FR-1.6 | Slab target/completed/delay fields | `SlabTargetDate`, `SlabCompletedDate` on `SiteQuery` + form fields. | ✅ |

## Module 2 — Search & Resolve

| ID | Requirement | Implementation | Status |
|---|---|---|---|
| FR-2.1 | Search by IPO, project name, or keyword | `QuerySearchRequest` + `QueryService.SearchAsync` (term matches IPO/product/comment; indexed FK columns). | ✅ |
| FR-2.2 | Filter by issue type / status (AND) | `QuerySearchRequest` `IssueType`, `Status`, `ProjectId`, `IpoId`, `ProductId`, `SortBy` — combined with AND. | ✅ |
| FR-2.3 | Only Manager marks Resolved | `Resolve` action gated by `[Authorize(Policy = "ManagerOnly")]`; Resolve button hidden for SiteEngineer in view. | ✅ |
| FR-2.4 | Every resolve action timestamped + user-tied | `QueryStatusHistory` + `AuditLogs` on resolve. | ✅ |
| FR-2.5 | Site role comments on open query | `QueryComment` entity, `QueriesController.Comment` action. | ✅ |

## Module 3 — Manager Dashboard

| ID | Requirement | Implementation | Status |
|---|---|---|---|
| FR-3.1 | Open delays ranked by severity | `DashboardService` (Manager view) — open items sorted by delay/severity descending. | ✅ |
| FR-3.2 | Breakdown by issue type (4 categories) | Dashboard count by `IssueType`. | ✅ |
| FR-3.3 | Raised-by + days open | `RaisedByUser` included in dashboard rows; delay days computed. | ✅ |
| FR-3.4 | Real-time updates | Server-rendered; poll-based refresh on PWA dashboard (in-app check). True push deferred (Phase 3). | 🔶 |

## Module 4 — Product Code Lookup

| ID | Requirement | Implementation | Status |
|---|---|---|---|
| FR-4.1 | Central product catalogue upload | `ProductService.SeedDefaultCatalogueAsync` (in-app `AccessoryCatalogue`), Excel/CSV/JSON `ImportAsync`, template download. Searchable immediately after upload. | ✅ |
| FR-4.2 | Search/scan to confirm product | `ProductsController.Index` search by code/name (`?term=`), product detail. | ✅ |
| FR-4.3 | Query links to verified product code | `CreateQuery` stores `ProductCode`/`ProductId` selected from catalogue (search-and-pick UI in Create view). | ✅ |

## Module 5 — Auto-Generated Email Templates

| ID | Requirement | Implementation | Status |
|---|---|---|---|
| FR-5.1 | Email auto-fills IPO, project, issue type, sender | `EmailService.PreviewAsync` renders placeholders from the linked query (project, IPO, issue type, raised-by) into `EmailRecord`. | ✅ |
| FR-5.2 | Template wording per issue type | One default `EmailTemplate` per issue type, seeded; `Templates`/`EditTemplate` admin UI. | ✅ |
| FR-5.3 | Editable before sending | Compose page pre-fills and allows editing; SaveDraft → Send flow. | ✅ |

## Non-Functional Requirements

| ID | Requirement | Implementation | Status |
|---|---|---|---|
| NFR-1 | iOS + Android | PWA (browser + installable) — works on both platforms. | ✅ |
| NFR-2 | Search < 2s up to 500 open records | EF queries on indexed columns (`TenantId`, FK indexes, `IX_Products_TenantId_ProductCode`). | ✅ |
| NFR-3 | Audit log with identity + timestamp | `AuditLogs` + entity-level `CreatedBy/UpdatedBy`, `QueryStatusHistory`, `EotStatusHistory`. | ✅ |
| NFR-4 | Photo retention period | `Features:Photos:RetentionMonths` config (0 = keep); when > 0 the 6-hourly `MaintenanceBackgroundService` purges expired `QueryPhotos` and deletes stored files. | ✅ |
| NFR-5 | RBAC enforced at API level, not just UI | `[Authorize(Policy=...)]` on all controller actions; verified: SiteEngineer gets 302 → Access Denied for `/Users` and `/Reports`. | ✅ |

## Data Requirements (BRD Section 7)

| Tracker Column | App field | Status |
|---|---|---|
| IPO | `SiteQuery.IpoNumber` / `IpoId` (selectable IPO) | ✅ |
| Project | `SiteQuery.ProjectId` (linked to catalogue) | ✅ |
| Issue | `IssueType` enum (4 categories) | ✅ |
| Qty (Nos), Qty (SQM) | `QuantityNos`, `QuantitySqm` | ✅ |
| Dispatch Status | `DispatchStatus` (Pending/Dispatched/Partial) + `QueryStatus` | ✅ |
| Delay | computed delay days in list/detail/reports | ✅ |
| Slab Target / Completed / Delay | `SlabTargetDate`, `SlabCompletedDate` | ✅ |

## BRD Roadmap — beyond Phase 1

| Feature | Phase | Status |
|---|---|---|
| Assign to a named owner | 2 | ⏳ (multi-user + roles exist; explicit assignment UI deferred) |
| Offline capture + sync | 2 | ⏳ (PWA cache only) |
| Photo markup | 2 | ⏳ |
| Location pinning | 2 | ⏳ |
| Escalation timer | 2 | ✅ (`Features:Escalation` enabled, `Days=10`, `Role=TenantAdmin`; `MaintenanceBackgroundService` escalates overdue queries + notifies TenantAdmin, verified live) |
| Voice-to-text, Push notifications, Bulk resolve, Multi-photo | 3 | ⏳ |
| Analytics/trend + exportable historical reports | 4 | 🔶 (Reports module + Excel export already cover much of this) |

## SaaS extensions implemented (beyond BRD Phase 1, per session scope)

- Multi-tenant tenancy (Tenants, tenant filter, host-header resolution), Super Admin back office (tenants, plans, usage).
- Subscription plans, trial, usage counters, expiry background worker.
- Users management + 4 roles, notifications, EOT management, Reports suite with Excel export.
