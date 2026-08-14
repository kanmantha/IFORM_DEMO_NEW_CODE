# Architecture — I-FORM Site Query & Defect Tracking

## 1. Overview

Multi-tenant, mobile-first ASP.NET Core MVC / PWA for site query & defect tracking for I-FORM Aluminium & Design LLP. Tenants log in to record site queries, photos, EOTs and email notifications, with a Manager dashboard, product lookup, subscriptions, and a Super Admin back office.

- Target framework: `net10.0` (only .NET SDK 10.x is available on the build machine).
- Solution file: `IFORM.slnx`. Runs locally with SQL Server LocalDB.
- UI is server-rendered MVC with Bootstrap 5 + Bootstrap Icons, service-worker PWA, responsive (mobile-first) layout.

## 2. Solution Layout

| Project | Responsibility |
|---|---|
| `src/IForm.Contracts` | Shared contracts: `ICurrentUser`, `IFileStorageService`, storage DTOs. No dependencies. |
| `src/IForm.Domain` | Pure domain: entities (`Entities/`), `Enums/`, `Common/` (`TenantEntity`, `BaseEntity`, audit interfaces), `Exceptions/`. No external deps. |
| `src/IForm.Application` | Application layer: service interfaces + implementations, DTOs, business rules (`QueryBusinessRules`, `AccessoryCatalogue`). References Domain + Contracts. |
| `src/IForm.Persistence` | EF Core `AppDbContext` (tenant filter, auditing), migrations, `DbSeeder`, `DependencyInjection` extension. |
| `src/IForm.Infrastructure` | Pluggable infra: `LocalFileStorageService` / `AzureBlobFileStorageService`, email provider (`Log`/`Smtp`), mock payment. |
| `src/IForm.Web` | ASP.NET Core MVC app: controllers, Views, PWA assets, middleware, `Program.cs`. |
| `tests/IForm.UnitTests` | xUnit + Shouldly unit tests. |
| `tests/IForm.IntegrationTests` | Integration tests against a test database / WebApplicationFactory. |
| `tests/IForm.SecurityTests` | Security-focused tests (tenant isolation, authorization, file access). |

Dependency direction: `Web → Application → Domain ← Persistence/Infrastructure`. `Contracts` is referenced by all.

## 3. Multi-Tenancy

### Model
- Every tenant-scoped entity inherits `TenantEntity` (`TenantId` + auditing props) in `IForm.Domain/Common`.
- The `platform` tenant has `Id = Guid.Empty` and hosts `SuperAdmin` users (users are NOT tenant-filtered; their scope is enforced in `UserManagementService`).

### Tenant filter (`src/IForm.Persistence/AppDbContext.cs`)
- `HasQueryFilter` is configured for every `TenantEntity`:
  `IsSuperAdmin || TenantId == CurrentTenantId`
- The filter is **parameterized per query**: it references the context instance's `CurrentTenantId` and `IsSuperAdmin` properties (set by `TenantContextMiddleware`), so EF emits `@ef_filter__CurrentTenantId` / `@ef_filter__IsSuperAdmin` parameters instead of baking constants. This was a critical fix — a previously cached constant made every request query the wrong tenant after the first one.
- `ApplicationUser` is excluded from the tenant filter (Identity table shared across tenants; scoping happens in the user management service; Super Admin must see all users).
- Super Admin bypasses the tenant filter (needed for platform-level reports/usage).

### Flow per request
`TenantContextMiddleware` → resolves tenant from `ICurrentUser` (or host header via `TenantService.GetTenantIdByDomainAsync`) → sets `db.CurrentTenantId`/`IsSuperAdmin` on the scoped `AppDbContext` → all EF queries are scoped.

### Verified behaviour
- Two tenants + platform exist at runtime. A user of tenant A cannot see tenant B queries/projects (verified via a second tenant created through the Super Admin UI).
- Super Admin sees all tenants and all users.

## 4. Authentication & Authorization

- ASP.NET Core Identity with `Guid` keys; cookie auth; `UserName` login (demo users: `admin`/`Admin@12345`, `superadmin`/`Admin@12345`).
- Roles: `TenantAdmin`, `Manager`, `SiteEngineer`, `SuperAdmin`.
- Policies (in `Program.cs`):
  - `TenantUsers` — TenantAdmin, Manager, SiteEngineer
  - `TenantAdmin` — TenantAdmin, SuperAdmin
  - `ManagerOnly` — TenantAdmin, Manager
  - `SuperAdminOnly` — SuperAdmin
- Controllers use policy attributes. `ReportsController` uses `[Authorize]` at class level + per-action policies (`ManagerOnly` for business reports, `SuperAdminOnly` for UsageReport).
- Verified: a Site Engineer is redirected to Access Denied for `/Users` (TenantAdmin) and `/Reports` (ManagerOnly).

## 5. Key Flows

### Query lifecycle
1. Site Engineer creates query (project, IPO, product, issue type, dispatch status, comments, optional photos). Number format `SQ-####`.
2. Photos are normalized (ImageSharp, max width 1600, JPEG) and stored via `IFileStorageService` under `queries/`.
3. `QueryBusinessRules.ClassifySeverity(delay, thresholds)` derives severity from age (thresholds from tenant settings; defaults Watch 7 / Delayed 15 / Critical 30 / Severe 45 days).
4. Query report (`Reports/QueryReport`) + Excel export (ClosedXML).

### Email
- Auto-generated templates per issue type (Missing / Production Mistake / Design Mistake / Dispatch Missing) with subject/body placeholders rendered per query.
- Compose → SaveDraft → Send (provider `Log` by default; SMTP config ready). Records in `EmailRecords`.

### EOT
- Manager raises an EOT (title, description, reason, required date) against a project. Number format `EOT-##`.
- Mandatory documents per category must be uploaded (`Eot/UploadDocument`) before approval submit; documents stored under `eot/`.

### Products & catalogue
- Per-tenant product catalogue. `SeedDefaultCatalogue` imports the in-app `AccessoryCatalogue` (~140 products, derived from the supplied I-FORM accessories PDF).
- Excel/CSV/JSON import (`Products/Import`), template download, category grouping, product-project mapping.

### Subscriptions
- `SubscriptionService` background worker: refreshes usage counters, processes trial expiry → downgrade/grace period, enforcement checks.
- Plan-based limits (projects/queries/products). Trial via `StartTrialAsync` (creates Subscription + SubscriptionTransaction with matching `SubscriptionId`).

### Reports
`ReportService` builds dictionary-row reports: Query, Delay, Engineer, ProductIssue, EOT, Usage (Super Admin). Exported to xlsx via ClosedXML in `ReportsController`.

## 6. Infrastructure Abstractions

- **Storage**: `IFileStorageService` — `Local` (root `storage/`) for dev, `Azure` (blob) for prod. Files are served only through `FilesController.Download`, which now verifies the path is referenced by a tenant-owned record (QueryPhoto / EotDocument / Product.PhotoPath) before streaming — closes a cross-tenant file read.
- **Email**: `EmailProvider` — `Log` (default) or `Smtp`.
- **Payment**: `Mock` provider; `PaymentService` records transactions.
- **Settings**: tenant settings key/value; `AppSettingsService` reads thresholds and feature flags.

## 7. Configuration (`appsettings.json`)

- `ConnectionStrings:DefaultConnection` — LocalDB `IFORM_SiteQuery`.
- `ApplicationBaseUrl` — base URL used for file URLs (https://localhost:7111).
- `Storage:Provider` — `Local` | `Azure`.
- `Email:Provider` — `Log` | `Smtp`.
- `Payment:Provider` — `Mock`.
- `Features:Severity` — Watch/Delayed/Critical/Severe day thresholds.
- `Seed:SuperAdminEmail/Password` — credentials for the seeded super admin.

## 8. Seeding (`DbSeeder`)

- Ensures the `platform` tenant (`Id = Guid.Empty`, slug `platform`) exists; self-heals a stray platform row by delete-and-reinsert.
- Creates a demo tenant (slug `i-form-aluminium`) with `admin`/`Admin@12345`.
- Seeds super admin (`superadmin`), default subscription plans, email templates, tenant settings.
- Demo tenant admin login: UserName `admin`, password `Admin@12345`.

## 9. PWA

- `wwwroot/manifest.json`, `wwwroot/sw.js` (basic cache-first for app shell), icons, and responsive meta tags. Verified all PWA assets serve 200.

## 10. Known Constraints / Notes

- Only .NET SDK 10.0.302 installed; `dotnet ef` works, `sqlcmd` does not.
- App is launched with the `http` launch profile → `http://localhost:5246`.
- `AutoMapper` was removed from the Application layer (unused, flagged vulnerable); DTO mapping is manual.
