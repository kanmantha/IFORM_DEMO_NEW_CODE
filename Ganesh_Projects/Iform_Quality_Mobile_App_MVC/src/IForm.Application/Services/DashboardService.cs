using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using IForm.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetManagerDashboardAsync(CancellationToken ct = default);
    Task<TenantDashboardDto> GetTenantDashboardAsync(CancellationToken ct = default);
    Task<SuperAdminDashboardDto> GetSuperAdminDashboardAsync(CancellationToken ct = default);
}

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantSettingsProvider _settings;

    public DashboardService(IApplicationDbContext db, ICurrentUser currentUser, ITenantSettingsProvider settings)
    {
        _db = db;
        _currentUser = currentUser;
        _settings = settings;
    }

    public async Task<DashboardDto> GetManagerDashboardAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var today = DateTime.UtcNow;
        var thresholds = _settings.GetSeverityThresholds(tenantId);
        var delayThresholds = new DelayThresholds(thresholds.Watch, thresholds.Delayed, thresholds.Critical, thresholds.Severe);

        var queries = await _db.Queries
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .Include(x => x.Project)
            .Include(x => x.RaisedByUser)
            .AsNoTracking()
            .ToListAsync(ct);

        var open = queries.Where(x => x.Status != QueryStatus.Resolved).ToList();

        var delays = queries.ToDictionary(x => x.Id,
            x => QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, today));

        var critical = open.Count(x => QueryBusinessRules.ClassifySeverity(delays[x.Id], delayThresholds) >= SeverityLevel.Critical);

        var avgDelay = open.Count == 0 ? 0 : open.Average(x => (double)delays[x.Id]);

        var resolved = queries.Where(x => x.Status == QueryStatus.Resolved && x.ResolvedDate.HasValue).ToList();
        var avgResolution = resolved.Count == 0
            ? 0
            : resolved.Average(x => (double)Math.Max(0, (x.ResolvedDate!.Value - x.RaisedDate).TotalDays));

        var projectsAffected = open.Select(x => x.ProjectId).Distinct().Count();

        var topProjects = open
            .GroupBy(x => new { x.ProjectId, Name = x.Project?.DisplayName ?? "Unknown" })
            .Select(g => new ProjectDelayDto(g.Key.ProjectId, g.Key.Name,
                g.Sum(x => delays[x.Id]), g.Count(), g.Max(x => delays[x.Id])))
            .OrderByDescending(x => x.MaxDelayDays)
            .Take(10)
            .ToList();

        var byIssueType = Enum.GetValues<IssueType>()
            .Select(t => new IssueTypeCountDto(t, queries.Count(x => x.IssueType == t)))
            .ToList();

        var byStatus = Enum.GetValues<QueryStatus>()
            .Select(s => new StatusCountDto(s, queries.Count(x => x.Status == s)))
            .ToList();

        var byMonth = queries
            .GroupBy(x => new { x.RaisedDate.Year, x.RaisedDate.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyCountDto($"{g.Key.Year}-{g.Key.Month:D2}", g.Count(), g.Count(x => x.Status == QueryStatus.Resolved)))
            .ToList();

        var engineerWise = queries
            .GroupBy(x => x.RaisedByUser?.FullName ?? "Unknown")
            .Select(g => new EngineerCountDto(g.Key,
                g.Count(x => x.Status != QueryStatus.Resolved),
                g.Count(x => x.Status == QueryStatus.Resolved),
                g.Count(),
                g.Average(x => (double)delays[x.Id])))
            .OrderByDescending(x => x.OpenQueries)
            .ToList();

        var aging = new List<AgingBucketDto>
        {
            new("0-7 days", open.Count(x => delays[x.Id] <= 7)),
            new("8-15 days", open.Count(x => delays[x.Id] > 7 && delays[x.Id] <= 15)),
            new("16-30 days", open.Count(x => delays[x.Id] > 15 && delays[x.Id] <= 30)),
            new("31-45 days", open.Count(x => delays[x.Id] > 30 && delays[x.Id] <= 45)),
            new("46+ days", open.Count(x => delays[x.Id] > 45))
        };

        return new DashboardDto(queries.Count, open.Count, queries.Count(x => x.Status == QueryStatus.Resolved),
            critical, Math.Round(avgDelay, 1), projectsAffected,
            topProjects, byIssueType, byStatus, byMonth, engineerWise, Math.Round(avgResolution, 1), aging);
    }

    public async Task<TenantDashboardDto> GetTenantDashboardAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenant();
        var today = DateTime.UtcNow;
        var thresholds = _settings.GetSeverityThresholds(tenantId);
        var delayThresholds = new DelayThresholds(thresholds.Watch, thresholds.Delayed, thresholds.Critical, thresholds.Severe);

        var queries = await _db.Queries
            .Where(x => x.TenantId == tenantId && !x.IsDeleted)
            .AsNoTracking()
            .ToListAsync(ct);

        var open = queries.Where(x => x.Status != QueryStatus.Resolved).ToList();
        var delays = queries.ToDictionary(x => x.Id, x => QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, today));
        var critical = open.Count(x => QueryBusinessRules.ClassifySeverity(delays[x.Id], delayThresholds) >= SeverityLevel.Critical);
        var avgDelay = open.Count == 0 ? 0 : open.Average(x => (double)delays[x.Id]);
        var resolved = queries.Where(x => x.Status == QueryStatus.Resolved && x.ResolvedDate.HasValue).ToList();
        var avgResolution = resolved.Count == 0 ? 0 : resolved.Average(x => (double)Math.Max(0, (x.ResolvedDate!.Value - x.RaisedDate).TotalDays));

        var users = await _db.Users.CountAsync(x => x.TenantId == tenantId && x.IsActive, ct);
        var projects = await _db.Projects.CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);
        var products = await _db.Products.CountAsync(x => x.TenantId == tenantId && !x.IsDeleted, ct);
        var storage = await _db.QueryPhotos.Where(x => x.TenantId == tenantId).SumAsync(x => (long)x.SizeBytes, ct);
        storage += await _db.Documents.Where(x => x.TenantId == tenantId).SumAsync(x => (long)x.SizeBytes, ct);

        return new TenantDashboardDto(queries.Count, open.Count, queries.Count(x => x.Status == QueryStatus.Resolved),
            critical, open.Select(x => x.ProjectId).Distinct().Count(), Math.Round(avgDelay, 1), Math.Round(avgResolution, 1),
            users, projects, queries.Count, products, storage);
    }

    public async Task<SuperAdminDashboardDto> GetSuperAdminDashboardAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants.AsNoTracking().ToListAsync(ct);
        var subscriptions = await _db.Subscriptions
            .Include(s => s.Plan)
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Trial)
            .AsNoTracking()
            .ToListAsync(ct);

        var totalUsers = await _db.Users.CountAsync(ct);
        var totalProjects = await _db.Projects.CountAsync(x => !x.IsDeleted, ct);
        var queries = await _db.Queries.Where(x => !x.IsDeleted).AsNoTracking().ToListAsync(ct);
        var openQueries = queries.Count(x => x.Status != QueryStatus.Resolved);
        var critical = queries.Count(x => x.Status != QueryStatus.Resolved &&
            QueryBusinessRules.ClassifySeverity(QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, DateTime.UtcNow)) >= SeverityLevel.Critical);

        var storage = await _db.QueryPhotos.SumAsync(x => (long)x.SizeBytes, ct);
        storage += await _db.Documents.SumAsync(x => (long)x.SizeBytes, ct);

        var distribution = subscriptions
            .GroupBy(x => x.Plan?.PlanName ?? "None")
            .Select(g => new PlanDistributionDto(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .ToList();

        var today = DateTime.UtcNow;
        return new SuperAdminDashboardDto(
            tenants.Count,
            tenants.Count(t => t.Status == TenantStatus.Active),
            tenants.Count(t => t.Status == TenantStatus.Trial || subscriptions.Any(s => s.TenantId == t.Id && s.Status == SubscriptionStatus.Trial)),
            subscriptions.Count(s => s.RenewalDate.HasValue && s.RenewalDate.Value < today),
            totalUsers, totalProjects, queries.Count, openQueries, critical, storage, distribution);
    }

    private Guid RequireTenant() =>
        _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");
}
