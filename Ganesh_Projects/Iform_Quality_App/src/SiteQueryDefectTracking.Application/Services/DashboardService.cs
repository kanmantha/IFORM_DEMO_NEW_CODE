using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.DTOs.Dashboard;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Application.Services;
using SiteQueryDefectTracking.Domain.Entities;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Application.Services;

public class DashboardService(
    IApplicationDbContext context,
    IDateTimeService clock) : IDashboardService
{
    public async Task<DashboardSnapshotDto> GetSnapshotAsync(CancellationToken ct = default)
    {
        var todayLocal = clock.AppNow.Date;

        var openQueries = await context.Queries
            .AsNoTracking()
            .Where(q => q.Status != QueryStatus.Resolved)
            .Include(q => q.Project)
            .Include(q => q.IssueType)
            .Include(q => q.VerifiedProductCode)
            .Include(q => q.RaisedByUser)
            .Include(q => q.ResolvedByUser)
            .Include(q => q.Attachments)
            .OrderByDescending(q => q.DelayDays)
            .ThenBy(q => q.RaiseDate)
            .ToListAsync(ct);

        var allQueries = await context.Queries.AsNoTracking()
            .Select(q => new { q.Status, q.RaiseDate, q.ResolvedDate, q.IssueTypeId, q.ProjectId })
            .ToListAsync(ct);

        var resolved = allQueries.Where(q => q.Status == QueryStatus.Resolved).ToList();
        var resolvedToday = resolved.Count(q => q.ResolvedDate.HasValue && q.ResolvedDate.Value.Date == todayLocal);

        var delayDays = openQueries.Select(q => q.DelayDays).ToList();

        var summary = new DashboardSummaryDto(
            TotalOpenQueries: openQueries.Count,
            Pending: openQueries.Count(q => q.Status == QueryStatus.Pending),
            InProgress: openQueries.Count(q => q.Status == QueryStatus.InProgress),
            ResolvedTotal: resolved.Count,
            ResolvedToday: resolvedToday,
            CriticalDelays: openQueries.Count(q => q.DelayDays >= Domain.Constants.DelayThresholds.Critical),
            AverageDelay: delayDays.Count > 0 ? delayDays.Average() : 0,
            MaxDelay: delayDays.Count > 0 ? delayDays.Max() : 0,
            TotalQueries: allQueries.Count);

        var openByIssueType = openQueries
            .GroupBy(q => new { q.IssueTypeId, Name = q.IssueType?.Name ?? "Unknown" })
            .ToDictionary(g => g.Key.IssueTypeId, g => new { Count = g.Count(), Delay = g.Sum(q => q.DelayDays) });

        var allIssueTypes = await context.IssueTypes
            .AsNoTracking()
            .Where(i => i.IsActive)
            .OrderBy(i => i.Name)
            .Select(i => new { i.Id, i.Name })
            .ToListAsync(ct);

        var issues = allIssueTypes
            .Select(i => openByIssueType.TryGetValue(i.Id, out var hit)
                ? new IssueBreakdownDto(i.Id, i.Name, hit.Count, hit.Delay)
                : new IssueBreakdownDto(i.Id, i.Name, 0, 0))
            .ToList();

        var projects = openQueries
            .GroupBy(q => new { q.ProjectId, Name = q.Project?.Name ?? "Unknown" })
            .Select(g => new ProjectBreakdownDto(
                g.Key.ProjectId,
                g.Key.Name,
                g.Count(),
                g.Average(q => q.DelayDays),
                g.Sum(q => q.DelayDays)))
            .OrderByDescending(p => p.OpenCount)
            .ToList();

        var statusDistribution = new List<StatusBreakdownDto>
        {
            new("Pending", openQueries.Count(q => q.Status == QueryStatus.Pending)),
            new("In Progress", openQueries.Count(q => q.Status == QueryStatus.InProgress)),
            new("Resolved", resolved.Count)
        };

        var buckets = new List<DelayBucketDto>
        {
            new("0", openQueries.Count(q => q.DelayDays == 0)),
            new("1-3", openQueries.Count(q => q.DelayDays >= 1 && q.DelayDays <= 3)),
            new("4-7", openQueries.Count(q => q.DelayDays >= 4 && q.DelayDays <= 7)),
            new("8-14", openQueries.Count(q => q.DelayDays >= 8 && q.DelayDays <= 14)),
            new("15+", openQueries.Count(q => q.DelayDays > 14))
        };

        return new DashboardSnapshotDto(
            summary,
            issues,
            projects,
            statusDistribution,
            buckets,
            openQueries.Select(QueryMappers.ToSummary).ToList());
    }

    public async Task<IReadOnlyList<QuerySummaryDto>> GetOpenQueriesAsync(CancellationToken ct = default)
    {
        var rows = await context.Queries
            .AsNoTracking()
            .Where(q => q.Status != QueryStatus.Resolved)
            .Include(q => q.Project)
            .Include(q => q.IssueType)
            .Include(q => q.VerifiedProductCode)
            .Include(q => q.RaisedByUser)
            .Include(q => q.ResolvedByUser)
            .Include(q => q.Attachments)
            .OrderByDescending(q => q.DelayDays)
            .ToListAsync(ct);

        return rows.Select(QueryMappers.ToSummary).ToList();
    }
}