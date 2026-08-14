using IForm.Domain.Common;
using IForm.Domain.Enums;

namespace IForm.Domain.Entities;

public class SubscriptionPlan : BaseEntity
{
    public string PlanName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public PlanTier Tier { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "INR";
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public int TrialDays { get; set; }
    public int MaxUsers { get; set; }
    public int MaxManagers { get; set; }
    public int MaxSiteEngineers { get; set; }
    public int MaxProjects { get; set; }
    public int MaxQueries { get; set; }
    public int MaxProducts { get; set; }
    public long MaxStorageBytes { get; set; }
    public bool AllowEot { get; set; }
    public bool AllowDocuments { get; set; }
    public bool AllowAdvancedReports { get; set; }
    public bool AllowAuditLogs { get; set; }
    public bool AllowApiAccess { get; set; }
    public bool AllowCustomBranding { get; set; }
    public bool AllowEmailTemplates { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class Subscription : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionPlan? Plan { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public DateTime TrialStartDate { get; set; }
    public DateTime? TrialEndDate { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? RenewalDate { get; set; }
    public DateTime? GracePeriodEndDate { get; set; }
    public DateTime? CancelledDate { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.FreeTrial;
    public bool AutoRenew { get; set; } = true;
    public string? ExternalCustomerId { get; set; }
    public string? ExternalSubscriptionId { get; set; }
}

public class SubscriptionTransaction : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    public SubscriptionAction Action { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus PaymentStatus { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

public class Invoice : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Subscription? Subscription { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public string? ExternalId { get; set; }
}

public class FeatureFlag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; } = true;
    public PlanTier MinimumTier { get; set; }
}

/// <summary>Denormalized usage counters refreshed by a background service and checked server-side for plan limits.</summary>
public class UsageCounter : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public int UserCount { get; set; }
    public int ManagerCount { get; set; }
    public int SiteEngineerCount { get; set; }
    public int ProjectCount { get; set; }
    public int QueryCount { get; set; }
    public int ProductCount { get; set; }
    public long StorageBytes { get; set; }
    public DateTime LastCalculatedAt { get; set; }
}
