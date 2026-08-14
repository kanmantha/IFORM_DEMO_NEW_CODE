using IForm.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Common.Interfaces;

/// <summary>
/// Unit-of-work contract implemented by the persistence DbContext so the
/// Application layer never depends on EF directly.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<TenantSetting> TenantSettings { get; }
    DbSet<ApplicationUser> Users { get; }
    DbSet<IdentityRole<Guid>> Roles { get; }
    DbSet<IdentityUserRole<Guid>> UserRoles { get; }
    DbSet<SubscriptionPlan> SubscriptionPlans { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<SubscriptionTransaction> SubscriptionTransactions { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<FeatureFlag> FeatureFlags { get; }
    DbSet<UsageCounter> UsageCounters { get; }
    DbSet<Project> Projects { get; }
    DbSet<Ipo> Ipos { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductProjectMapping> ProductProjectMappings { get; }
    DbSet<SiteQuery> Queries { get; }
    DbSet<QueryPhoto> QueryPhotos { get; }
    DbSet<QueryComment> QueryComments { get; }
    DbSet<QueryStatusHistory> QueryStatusHistory { get; }
    DbSet<EmailTemplate> EmailTemplates { get; }
    DbSet<EmailRecord> EmailRecords { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<Document> Documents { get; }
    DbSet<EotRecord> EotRecords { get; }
    DbSet<EotDocument> EotDocuments { get; }
    DbSet<EotStatusHistory> EotStatusHistory { get; }
    DbSet<ScopeVariation> ScopeVariations { get; }
    DbSet<ClientApproval> ClientApprovals { get; }
    DbSet<SystemSetting> SystemSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
