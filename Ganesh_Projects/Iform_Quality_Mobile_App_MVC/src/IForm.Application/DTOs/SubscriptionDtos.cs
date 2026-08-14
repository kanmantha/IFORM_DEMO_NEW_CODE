using IForm.Domain.Enums;

namespace IForm.Application.DTOs;

public record SubscriptionPlanDto(
    Guid Id, string PlanName, string? Description, PlanTier Tier, decimal Price, string Currency,
    BillingCycle BillingCycle, int TrialDays, int MaxUsers, int MaxManagers, int MaxSiteEngineers,
    int MaxProjects, int MaxQueries, int MaxProducts, long MaxStorageBytes,
    bool AllowEot, bool AllowDocuments, bool AllowAdvancedReports, bool AllowAuditLogs,
    bool AllowApiAccess, bool AllowCustomBranding, bool AllowEmailTemplates, bool IsActive);

public record SubscriptionDto(
    Guid Id, Guid PlanId, string PlanName, PlanTier Tier, decimal Price, string Currency, BillingCycle BillingCycle,
    SubscriptionStatus Status, DateTime TrialStartDate, DateTime? TrialEndDate, DateTime StartDate, DateTime? RenewalDate,
    DateTime? GracePeriodEndDate, PaymentStatus PaymentStatus, bool AutoRenew, int? DaysRemaining);

public record UsageDto(int UserCount, int ManagerCount, int SiteEngineerCount, int ProjectCount, int QueryCount, int ProductCount, long StorageBytes);

public record TenantSubscriptionViewDto(
    SubscriptionPlanDto? Plan, SubscriptionDto? Subscription, UsageDto Usage,
    int? TrialDaysRemaining, int? DaysRemaining,
    IReadOnlyList<SubscriptionPlanDto> AvailablePlans, bool CanUpgrade, bool CanDowngrade);

public record ChangePlanRequest(Guid PlanId);

public record CreateTenantRequest(string Name, string Email, string PlanName, string TenantAdminName, string TenantAdminEmail, string TenantAdminPassword);

public record TenantListItemDto(Guid Id, string Name, string Slug, string Email, TenantStatus Status, string? PlanName, SubscriptionStatus SubscriptionStatus, int UserCount, int ProjectCount, DateTime CreatedAt);

public record InvoiceDto(Guid Id, string InvoiceNumber, DateTime IssueDate, DateTime? DueDate, decimal Amount, string Currency, InvoiceStatus Status);
