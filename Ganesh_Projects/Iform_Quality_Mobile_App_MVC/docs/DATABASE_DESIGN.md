# Database Design — I-FORM Site Query

Database: `IFORM_SiteQuery` (SQL Server / LocalDB for dev, Azure SQL for prod). Schema is owned by EF Core migrations (`IForm.Persistence/Migrations/20260813000849_InitialCreate`).

## Conventions

- Every tenant-scoped table carries `TenantId` (`uniqueidentifier`, FK → `Tenants.Id`) plus audit columns `CreatedAt/CreatedBy/CreatedByUserId` and `UpdatedAt/UpdatedBy/UpdatedByUserId` from `TenantEntity` / `BaseEntity`.
- Soft delete via `IsDeleted` on most entities.
- Primary keys are client-generated `uniqueidentifier` (set by `BaseEntity`).
- `Tenant.Id` is configured `ValueGeneratedNever()` because the platform tenant uses the reserved `Guid.Empty` id.

## Tenancy tables

| Table | Notes |
|---|---|
| `Tenants` | `Id`, `Name`, `Slug` (unique), `Email`, `Status` (Trial/Active/Inactive). Platform tenant = `Guid.Empty`. |
| `TenantSettings` | Key/value per tenant (`features`, severity thresholds, etc.). |

## Identity tables (shared, NOT tenant-filtered)

`AspNetUsers` (has `TenantId`), `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`. Keyed with `Guid`. `ApplicationUser.TenantId` links a user to a tenant; the platform tenant hosts Super Admins.

## Domain tables

### Projects & IPOs
- `Projects` — code, name, client, contract, start/due dates, status, IPOs (1..n), `IsDeleted`.
- `Ipos` — per-project IPO numbers (`ProjectId` FK).

### Queries (Site Query / Defects)
- `SiteQueries` — `QueryNumber` (`SQ-####`, unique per tenant), `ProjectId`, `IpoId`, `ProductId` (nullable), product snapshot fields (code/name), `IssueType` (Missing/ProductionMistake/DesignMistake/DispatchMissing), `DispatchStatus` (Pending/Dispatched/Partial), `QuantityNos`, `Status`, `RaisedDate`, `RaisedByUserId`, `SlabCompletedDate`, `Comments`, `RaisedFrom`.
- `QueryPhotos` — photo per query (`QueryId` FK, `FilePath` (random-id based, stored under `queries/`), `FileName`, `ContentType`, `SizeBytes`, `UploadedByUserId`).
- `QueryComments` — discussion thread per query.
- `QueryStatusHistory` — status change audit per query.

### Products
- `ProductCategories` — per-tenant category name (unique per tenant).
- `Products` — `ProductCode` (unique per tenant via `IX_Products_TenantId_ProductCode`), name, specification, material, unit, description, `PhotoPath`, `Source` (`iform-catalogue`/`import`), `CategoryId` FK.
- `ProductProjectMappings` — many-to-many product ↔ project.

### EOT
- `EotRecords` — `EotNumber` (`EOT-##`), `ProjectId`, `Title`, `Description`, `Reason`, `RequiredDate`, `RequestedBy`, `Status`, approval fields.
- `EotDocuments` — mandatory documents per EOT category (`FilePath` under `eot/`, `ContentType`, `SizeBytes`).
- `ScopeVariations`, `EotStatusHistory`, `ClientApproval` — supporting records.

### Email
- `EmailTemplates` — one default template per issue type (`Name`, `Subject`, `Body`, `To/Cc/BccRecipients`, `IsDefault`).
- `EmailRecords` — sent/drafted emails (`QueryId` FK nullable, `To/Cc/Bcc/Subject/Body`, `IsDraft`, `Sent`, `SentAt`, `Error`). NOTE: `Bcc`/`Cc` columns are NOT NULL (service normalizes null → empty string).

### Notifications & Audit
- `Notifications` — per-user notifications (`UserId`, `IsRead`).
- `AuditLogs` — `Action`, entity name/id, old/new values, `UserId`.

### Subscriptions
- `SubscriptionPlans` — name, tier, price, trial days, limits (projects/queries/products), `IsActive`.
- `Subscriptions` — tenant's current plan (`TenantId`, `PlanId`, `Status` Trial/Active/Expired/..., `TrialStart/EndDate`, `RenewalDate`, `GracePeriodEndDate`, `PaymentStatus`, `AutoRenew`).
- `SubscriptionTransactions` — ledger (`SubscriptionId` FK — must reference the subscription id, not `Guid.Empty`).
- `UsageCounters` — per-tenant counts (projects/queries/products/photo storage) refreshed by the background worker.
- `SystemSettings` — key/value system config.

## Key indexes

- `IX_Products_TenantId_ProductCode` (unique) — also the constraint that failed during catalogue seed when duplicate codes existed in source data (now de-duplicated + the seeder tracks seen codes).
- `IX_Projects_TenantId_IsDeleted`, query/IPO/product tenant+FK indexes, `IX_TenantSettings_TenantId_Key`.

## Multi-tenant data integrity

- The EF tenant filter (`IsSuperAdmin || TenantId == CurrentTenantId`) applies to all `TenantEntity` tables. Because the filter uses per-request context properties, each DB round-trip is scoped to the logged-in tenant.
- Server-side integrity still relies on the filter (no row-level security yet — see OPEN_QUESTIONS.md).

## Concurrency / soft delete

- `RowVersion` (rowversion) on mutable entities for optimistic concurrency.
- `IsDeleted` filters on read paths (`Where(x => !x.IsDeleted)`); storage files for deleted records are cleaned by the delete service paths.

## Migration workflow

```powershell
cd src/IForm.Persistence
dotnet ef migrations add <Name> --project . --startup-project ../IForm.Web --context AppDbContext
dotnet ef database update --project . --startup-project ../IForm.Web --context AppDbContext
```
The app also auto-migrates and seeds on startup (`Program.cs`), so a fresh LocalDB is created automatically.
