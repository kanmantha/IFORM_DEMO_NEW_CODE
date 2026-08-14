using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface ISubscriptionService
{
    Task<SubscriptionDto?> GetCurrentAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlanDto>> GetAvailablePlansAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionPlanDto>> GetAllPlansAsync(CancellationToken ct = default);
    Task<UsageDto> GetUsageAsync(CancellationToken ct = default);
    Task<UsageDto> GetUsageAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSubscriptionViewDto> GetTenantViewAsync(CancellationToken ct = default);
    Task AssertCanAddUsersAsync(string role, CancellationToken ct = default);
    Task AssertCanAddProjectsAsync(CancellationToken ct = default);
    Task AssertCanAddQueriesAsync(CancellationToken ct = default);
    Task AssertCanAddProductsAsync(CancellationToken ct = default);
    Task<Guid> StartTrialAsync(Guid tenantId, Guid planId, CancellationToken ct = default);
    Task ChangePlanAsync(Guid planId, CancellationToken ct = default);
    Task ProcessExpirationsAsync(CancellationToken ct = default);
    Task RefreshUsageAsync(Guid tenantId, CancellationToken ct = default);
    Task RefreshAllUsageAsync(CancellationToken ct = default);
    Task AssertTenantActiveAsync(CancellationToken ct = default);
}

public class SubscriptionService : ISubscriptionService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public SubscriptionService(IApplicationDbContext db, ICurrentUser currentUser, IAuditLogger audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task AssertTenantActiveAsync(CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue) throw new AuthorizationException("Tenant context is missing.");
        var tenant = await _db.Tenants.FindAsync(new object?[] { tenantId.Value }, ct);
        if (tenant == null) throw new NotFoundException("Tenant not found.");
        if (tenant.Status == TenantStatus.Suspended || tenant.Status == TenantStatus.Inactive)
            throw new AuthorizationException("This tenant has been suspended. Contact the administrator.");
    }

    public async Task<SubscriptionDto?> GetCurrentAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var sub = await _db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
        return sub is null ? null : Map(sub);
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetAvailablePlansAsync(CancellationToken ct = default)
    {
        var plans = await _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Price)
            .ToListAsync(ct);
        return plans.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetAllPlansAsync(CancellationToken ct = default)
    {
        var plans = await _db.SubscriptionPlans.OrderBy(p => p.SortOrder).ThenBy(p => p.Price).ToListAsync(ct);
        return plans.Select(Map).ToList();
    }

    public async Task<UsageDto> GetUsageAsync(CancellationToken ct = default) => await GetUsageAsync(RequireTenant(), ct);

    public async Task<UsageDto> GetUsageAsync(Guid tenantId, CancellationToken ct = default)
    {
        var userCount = await _db.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive, ct);
        var managers = await _db.Users
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Join(_db.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
            .Join(_db.Roles, x => x.ur.RoleId, r => r.Id, (x, r) => new { x.u, r.Name })
            .CountAsync(x => x.Name == AppRoles.Manager, ct);
        var engineers = await _db.Users
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .Join(_db.UserRoles, u => u.Id, ur => ur.UserId, (u, ur) => new { u, ur })
            .Join(_db.Roles, x => x.ur.RoleId, r => r.Id, (x, r) => new { x.u, r.Name })
            .CountAsync(x => x.Name == AppRoles.SiteEngineer, ct);
        var projectCount = await _db.Projects.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);
        var queryCount = await _db.Queries.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);
        var productCount = await _db.Products.IgnoreQueryFilters().CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);
        var storage = await _db.QueryPhotos.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).SumAsync(x => (long)x.SizeBytes, ct);
        storage += await _db.Documents.IgnoreQueryFilters().Where(x => x.TenantId == tenantId).SumAsync(x => (long)x.SizeBytes, ct);

        return new UsageDto(userCount, managers, engineers, projectCount, queryCount, productCount, storage);
    }

    public async Task<TenantSubscriptionViewDto> GetTenantViewAsync(CancellationToken ct = default)
    {
        var sub = await GetCurrentAsync(ct);
        var usage = await GetUsageAsync(ct);
        var plans = await GetAvailablePlansAsync(ct);

        if (sub is null)
            return new TenantSubscriptionViewDto(null, null, usage, null, null, plans, false, false);

        var currentPlan = plans.FirstOrDefault(p => p.Id == sub.PlanId);
        var daysRemaining = sub.RenewalDate.HasValue
            ? Math.Max(0, (sub.RenewalDate.Value.Date - DateTime.UtcNow.Date).Days)
            : (int?)null;

        return new TenantSubscriptionViewDto(currentPlan, sub, usage,
            sub.Status == SubscriptionStatus.Trial && sub.TrialEndDate.HasValue
                ? Math.Max(0, (sub.TrialEndDate.Value.Date - DateTime.UtcNow.Date).Days)
                : null,
            daysRemaining, plans, true, true);
    }

    public async Task AssertCanAddUsersAsync(string role, CancellationToken ct = default)
    {
        var (plan, usage) = await LoadPlanAndUsageAsync(ct);
        if (plan.MaxUsers > 0 && usage.UserCount >= plan.MaxUsers)
            throw new PlanLimitExceededException($"User limit reached ({usage.UserCount}/{plan.MaxUsers}). Upgrade your plan to add more users.");

        if (role == AppRoles.Manager && plan.MaxManagers > 0 && usage.ManagerCount >= plan.MaxManagers)
            throw new PlanLimitExceededException($"Manager limit reached ({usage.ManagerCount}/{plan.MaxManagers}).");
        if (role == AppRoles.SiteEngineer && plan.MaxSiteEngineers > 0 && usage.SiteEngineerCount >= plan.MaxSiteEngineers)
            throw new PlanLimitExceededException($"Site Engineer limit reached ({usage.SiteEngineerCount}/{plan.MaxSiteEngineers}).");
    }

    public async Task AssertCanAddProjectsAsync(CancellationToken ct = default)
    {
        var (plan, usage) = await LoadPlanAndUsageAsync(ct);
        if (plan.MaxProjects > 0 && usage.ProjectCount >= plan.MaxProjects)
            throw new PlanLimitExceededException($"Project limit reached ({usage.ProjectCount}/{plan.MaxProjects}). Upgrade your plan to add more projects.");
    }

    public async Task AssertCanAddQueriesAsync(CancellationToken ct = default)
    {
        var (plan, usage) = await LoadPlanAndUsageAsync(ct);
        if (plan.MaxQueries > 0 && usage.QueryCount >= plan.MaxQueries)
            throw new PlanLimitExceededException($"Query limit reached ({usage.QueryCount}/{plan.MaxQueries}). Upgrade your plan to raise more queries.");
    }

    public async Task AssertCanAddProductsAsync(CancellationToken ct = default)
    {
        var (plan, usage) = await LoadPlanAndUsageAsync(ct);
        if (plan.MaxProducts > 0 && usage.ProductCount >= plan.MaxProducts)
            throw new PlanLimitExceededException($"Product limit reached ({usage.ProductCount}/{plan.MaxProducts}). Upgrade your plan to add more products.");
    }

    public async Task<Guid> StartTrialAsync(Guid tenantId, Guid planId, CancellationToken ct = default)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(new object?[] { planId }, ct)
            ?? throw new NotFoundException("Plan not found.");

        var trialDays = plan.TrialDays > 0 ? plan.TrialDays : 14;
        var start = DateTime.UtcNow;
        var sub = new Subscription
        {
            TenantId = tenantId,
            PlanId = planId,
            Status = SubscriptionStatus.Trial,
            TrialStartDate = start,
            TrialEndDate = start.AddDays(trialDays),
            StartDate = start,
            RenewalDate = start.AddDays(trialDays),
            GracePeriodEndDate = start.AddDays(trialDays + 7),
            PaymentStatus = PaymentStatus.FreeTrial,
            AutoRenew = true
        };
        _db.Subscriptions.Add(sub);
        _db.SubscriptionTransactions.Add(new SubscriptionTransaction
        {
            TenantId = tenantId,
            SubscriptionId = sub.Id,
            Action = SubscriptionAction.TrialStarted,
            Amount = 0,
            PaymentStatus = PaymentStatus.FreeTrial,
            Notes = $"Trial started on {plan.PlanName}"
        });
        await _db.SaveChangesAsync(ct);

        var tenant = await _db.Tenants.FindAsync(new object?[] { tenantId }, ct);
        if (tenant != null)
        {
            tenant.Status = TenantStatus.Trial;
            await _db.SaveChangesAsync(ct);
        }

        return sub.Id;
    }

    public async Task ChangePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var plan = await _db.SubscriptionPlans.FindAsync(new object?[] { planId }, ct)
            ?? throw new NotFoundException("Plan not found.");

        var current = await _db.Subscriptions
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        var action = SubscriptionAction.Upgraded;
        if (current != null && current.PlanId != planId)
        {
            var currentPlan = await _db.SubscriptionPlans.FindAsync(new object?[] { current.PlanId }, ct);
            if (currentPlan != null && currentPlan.Tier > plan.Tier) action = SubscriptionAction.Downgraded;
        }

        if (current == null)
        {
            var start = DateTime.UtcNow;
            var days = plan.BillingCycle == BillingCycle.Yearly ? 365 : 30;
            current = new Subscription
            {
                TenantId = tenantId,
                PlanId = planId,
                Status = SubscriptionStatus.Active,
                TrialStartDate = start,
                StartDate = start,
                RenewalDate = start.AddDays(days),
                PaymentStatus = PaymentStatus.Pending,
                AutoRenew = true
            };
            _db.Subscriptions.Add(current);
        }
        else
        {
            current.PlanId = planId;
            current.Status = SubscriptionStatus.Active;
            if (plan.BillingCycle != current.Plan.BillingCycle || !current.RenewalDate.HasValue || current.RenewalDate < DateTime.UtcNow)
                current.RenewalDate = DateTime.UtcNow.AddDays(plan.BillingCycle == BillingCycle.Yearly ? 365 : 30);
            current.PaymentStatus = PaymentStatus.Pending;
            current.TrialEndDate = null;
        }

        _db.SubscriptionTransactions.Add(new SubscriptionTransaction
        {
            TenantId = tenantId,
            SubscriptionId = current.Id,
            Action = action,
            Amount = plan.Price,
            Currency = plan.Currency,
            PaymentStatus = PaymentStatus.Pending,
            Notes = $"Plan changed to {plan.PlanName}"
        });

        await _db.SaveChangesAsync(ct);

        var tenant = await _db.Tenants.FindAsync(new object?[] { tenantId }, ct);
        if (tenant != null)
        {
            tenant.Status = TenantStatus.Active;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync("Subscription Changed", nameof(Subscription), current.Id.ToString(), null, plan.PlanName, ct);
    }

    public async Task ProcessExpirationsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Trial expiry: move to Active with pending payment, or suspend if grace passed.
        var expiredTrials = await _db.Subscriptions.IgnoreQueryFilters()
            .Where(s => s.Status == SubscriptionStatus.Trial && s.TrialEndDate.HasValue && s.TrialEndDate.Value < now)
            .Include(s => s.Tenant)
            .ToListAsync(ct);

        foreach (var sub in expiredTrials)
        {
            if (sub.GracePeriodEndDate.HasValue && sub.GracePeriodEndDate.Value > now)
            {
                sub.Status = SubscriptionStatus.GracePeriod;
                if (sub.Tenant != null) sub.Tenant.Status = TenantStatus.Active;
            }
            else
            {
                sub.Status = SubscriptionStatus.Expired;
                if (sub.Tenant != null) sub.Tenant.Status = TenantStatus.Inactive;
            }
        }

        // Active subscription expiry.
        var expired = await _db.Subscriptions.IgnoreQueryFilters()
            .Where(s => (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.GracePeriod) &&
                        s.RenewalDate.HasValue && s.RenewalDate.Value < now)
            .Include(s => s.Tenant)
            .ToListAsync(ct);

        foreach (var sub in expired)
        {
            if (sub.GracePeriodEndDate.HasValue && sub.GracePeriodEndDate.Value > now)
            {
                sub.Status = SubscriptionStatus.GracePeriod;
            }
            else
            {
                sub.Status = SubscriptionStatus.Expired;
                if (sub.Tenant != null) sub.Tenant.Status = TenantStatus.Inactive;
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task RefreshUsageAsync(Guid tenantId, CancellationToken ct = default)
    {
        var usage = await GetUsageAsync(tenantId, ct);
        var counter = await _db.UsageCounters.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
        if (counter == null)
        {
            counter = new UsageCounter { TenantId = tenantId };
            _db.UsageCounters.Add(counter);
        }

        counter.UserCount = usage.UserCount;
        counter.ManagerCount = usage.ManagerCount;
        counter.SiteEngineerCount = usage.SiteEngineerCount;
        counter.ProjectCount = usage.ProjectCount;
        counter.QueryCount = usage.QueryCount;
        counter.ProductCount = usage.ProductCount;
        counter.StorageBytes = usage.StorageBytes;
        counter.LastCalculatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
    }

    public async Task RefreshAllUsageAsync(CancellationToken ct = default)
    {
        var tenantIds = await _db.Tenants.Where(t => t.Id != Guid.Empty).Select(t => t.Id).ToListAsync(ct);
        foreach (var tenantId in tenantIds)
            await RefreshUsageAsync(tenantId, ct);
    }

    private async Task<(SubscriptionPlan Plan, UsageDto Usage)> LoadPlanAndUsageAsync(CancellationToken ct)
    {
        var tenantId = RequireTenant();
        var sub = await _db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (sub?.Plan == null)
            throw new PlanLimitExceededException("No active subscription. Please subscribe to a plan.");

        if (sub.Status == SubscriptionStatus.Expired || sub.Status == SubscriptionStatus.Suspended)
            throw new PlanLimitExceededException("Subscription is not active. Please renew your plan.");

        var usage = await GetUsageAsync(tenantId, ct);
        return (sub.Plan, usage);
    }

    private Guid RequireTenant() => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    private static SubscriptionPlanDto Map(SubscriptionPlan p) => new(
        p.Id, p.PlanName, p.Description, p.Tier, p.Price, p.Currency, p.BillingCycle, p.TrialDays,
        p.MaxUsers, p.MaxManagers, p.MaxSiteEngineers, p.MaxProjects, p.MaxQueries, p.MaxProducts, p.MaxStorageBytes,
        p.AllowEot, p.AllowDocuments, p.AllowAdvancedReports, p.AllowAuditLogs, p.AllowApiAccess,
        p.AllowCustomBranding, p.AllowEmailTemplates, p.IsActive);

    private static SubscriptionDto Map(Subscription s) => new(
        s.Id, s.PlanId, s.Plan?.PlanName ?? "Unknown", s.Plan?.Tier ?? PlanTier.Free, s.Plan?.Price ?? 0,
        s.Plan?.Currency ?? "INR", s.Plan?.BillingCycle ?? BillingCycle.Monthly, s.Status,
        s.TrialStartDate, s.TrialEndDate, s.StartDate, s.RenewalDate, s.GracePeriodEndDate,
        s.PaymentStatus, s.AutoRenew,
        s.RenewalDate.HasValue ? Math.Max(0, (s.RenewalDate.Value.Date - DateTime.UtcNow.Date).Days) : null);
}
