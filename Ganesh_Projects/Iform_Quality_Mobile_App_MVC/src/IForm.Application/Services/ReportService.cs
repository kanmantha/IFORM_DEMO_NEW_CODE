using IForm.Application.Common.Interfaces;
using IForm.Application.DTOs;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using IForm.Domain.Exceptions;
using IForm.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

public record ReportRow(Dictionary<string, object> Values);

public interface IReportService
{
    Task<IReadOnlyList<ReportRow>> BuildQueryReportAsync(QuerySearchRequest filter, CancellationToken ct = default);
    Task<IReadOnlyList<ReportRow>> BuildDelayReportAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReportRow>> BuildEngineerReportAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReportRow>> BuildProductIssueReportAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReportRow>> BuildEotReportAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ReportRow>> BuildUsageReportAsync(CancellationToken ct = default);
}

public class ReportService : IReportService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ITenantSettingsProvider _settings;

    public ReportService(IApplicationDbContext db, ICurrentUser currentUser, ITenantSettingsProvider settings)
    {
        _db = db;
        _currentUser = currentUser;
        _settings = settings;
    }

    private Guid Tenant => _currentUser.TenantId ?? throw new AuthorizationException("Tenant context is missing.");

    public async Task<IReadOnlyList<ReportRow>> BuildQueryReportAsync(QuerySearchRequest filter, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var queries = await _db.Queries
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.Project)
            .Include(x => x.RaisedByUser)
            .AsNoTracking()
            .ToListAsync(ct);

        var rows = queries.Select(x =>
        {
            var delay = QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, today);
            return new ReportRow(new Dictionary<string, object>
            {
                ["Query Number"] = x.QueryNumber,
                ["IPO"] = x.IpoNumber,
                ["Project"] = x.Project?.DisplayName ?? string.Empty,
                ["Product Code"] = x.ProductCode ?? string.Empty,
                ["Product Name"] = x.ProductName ?? string.Empty,
                ["Issue Type"] = x.IssueType.ToString(),
                ["Qty (Nos)"] = (object?)x.QuantityNos ?? string.Empty,
                ["Qty (SQM)"] = (object?)x.QuantitySqm ?? string.Empty,
                ["Dispatch Status"] = x.DispatchStatus.ToString(),
                ["Status"] = x.Status.ToString(),
                ["Delay (Days)"] = delay,
                ["Raised By"] = x.RaisedByUser?.FullName ?? string.Empty,
                ["Raised Date"] = x.RaisedDate.ToString("yyyy-MM-dd"),
                ["Resolved Date"] = x.ResolvedDate?.ToString("yyyy-MM-dd") ?? string.Empty
            });
        }).ToList();

        return rows;
    }

    public async Task<IReadOnlyList<ReportRow>> BuildDelayReportAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var open = await _db.Queries
            .Where(x => x.TenantId == Tenant && !x.IsDeleted && x.Status != QueryStatus.Resolved)
            .Include(x => x.Project)
            .Include(x => x.RaisedByUser)
            .AsNoTracking()
            .ToListAsync(ct);

        var rows = open
            .Select(x =>
            {
                var delay = QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, today);
                return new { Item = x, Delay = delay };
            })
            .OrderByDescending(x => x.Delay)
            .Select(x => new ReportRow(new Dictionary<string, object>
            {
                ["IPO"] = x.Item.IpoNumber,
                ["Project"] = x.Item.Project?.DisplayName ?? string.Empty,
                ["Issue Type"] = x.Item.IssueType.ToString(),
                ["Delay (Days)"] = x.Delay,
                ["Severity"] = QueryBusinessRules.ClassifySeverity(x.Delay).ToString(),
                ["Raised By"] = x.Item.RaisedByUser?.FullName ?? string.Empty,
                ["Raised Date"] = x.Item.RaisedDate.ToString("yyyy-MM-dd"),
                ["Status"] = x.Item.Status.ToString()
            }))
            .ToList();

        return rows;
    }

    public async Task<IReadOnlyList<ReportRow>> BuildEngineerReportAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var queries = await _db.Queries
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.RaisedByUser)
            .AsNoTracking()
            .ToListAsync(ct);

        return queries
            .GroupBy(x => x.RaisedByUser?.FullName ?? "Unknown")
            .Select(g => new ReportRow(new Dictionary<string, object>
            {
                ["Engineer"] = g.Key,
                ["Total Queries"] = g.Count(),
                ["Open"] = g.Count(x => x.Status != QueryStatus.Resolved),
                ["Resolved"] = g.Count(x => x.Status == QueryStatus.Resolved),
                ["Avg Delay (Days)"] = Math.Round(g.Average(x => (double)QueryBusinessRules.CalculateDelayDays(x.RaisedDate, x.ResolvedDate, today)), 1),
                ["Missing"] = g.Count(x => x.IssueType == IssueType.Missing),
                ["Production Mistake"] = g.Count(x => x.IssueType == IssueType.ProductionMistake),
                ["Design Mistake"] = g.Count(x => x.IssueType == IssueType.DesignMistake),
                ["Dispatch Missing"] = g.Count(x => x.IssueType == IssueType.DispatchMissing)
            }))
            .OrderByDescending(x => (int)x.Values["Open"])
            .ToList();
    }

    public async Task<IReadOnlyList<ReportRow>> BuildProductIssueReportAsync(CancellationToken ct = default)
    {
        var queries = await _db.Queries
            .Where(x => x.TenantId == Tenant && !x.IsDeleted && x.ProductId != null)
            .Include(x => x.Project)
            .AsNoTracking()
            .ToListAsync(ct);

        return queries
            .GroupBy(x => x.ProductCode ?? "Unknown")
            .Select(g => new ReportRow(new Dictionary<string, object>
            {
                ["Product Code"] = g.Key,
                ["Product Name"] = g.First().ProductName ?? string.Empty,
                ["Total Issues"] = g.Count(),
                ["Open"] = g.Count(x => x.Status != QueryStatus.Resolved),
                ["Missing"] = g.Count(x => x.IssueType == IssueType.Missing),
                ["Production Mistake"] = g.Count(x => x.IssueType == IssueType.ProductionMistake),
                ["Design Mistake"] = g.Count(x => x.IssueType == IssueType.DesignMistake),
                ["Dispatch Missing"] = g.Count(x => x.IssueType == IssueType.DispatchMissing)
            }))
            .OrderByDescending(x => (int)x.Values["Total Issues"])
            .ToList();
    }

    public async Task<IReadOnlyList<ReportRow>> BuildEotReportAsync(CancellationToken ct = default)
    {
        var eots = await _db.EotRecords
            .Where(x => x.TenantId == Tenant && !x.IsDeleted)
            .Include(x => x.Project)
            .AsNoTracking()
            .ToListAsync(ct);

        return eots
            .OrderBy(x => x.EotNumber)
            .Select(x => new ReportRow(new Dictionary<string, object>
            {
                ["EOT No."] = x.EotNumber,
                ["Project"] = x.Project?.DisplayName ?? string.Empty,
                ["Client EOT No."] = x.ClientEotNumber,
                ["Financial Year"] = x.FinancialYear,
                ["Revision"] = x.RevisionNumber,
                ["Scenario"] = x.Scenario.ToString(),
                ["SPA Date"] = x.SpaDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ["Design Revision Date"] = x.DesignRevisionDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ["Scope Variation"] = x.ScopeVariations.Sum(v => v.NetScopeVariation),
                ["Delay (Days)"] = (object?)x.DelayDays ?? string.Empty,
                ["Cost Escalation"] = (object?)x.CostEscalation ?? string.Empty,
                ["Submission Status"] = x.SubmissionStatus.ToString(),
                ["Client Approval"] = x.ClientApproval.ToString(),
                ["Remarks"] = x.Remarks ?? string.Empty
            }))
            .ToList();
    }

    public async Task<IReadOnlyList<ReportRow>> BuildUsageReportAsync(CancellationToken ct = default)
    {
        var tenants = await _db.Tenants.AsNoTracking().ToListAsync(ct);
        var subs = await _db.Subscriptions.Include(s => s.Plan).AsNoTracking().ToListAsync(ct);

        var rows = new List<ReportRow>();
        foreach (var tenant in tenants)
        {
            var sub = subs.FirstOrDefault(s => s.TenantId == tenant.Id);
            var users = await _db.Users.CountAsync(u => u.TenantId == tenant.Id && u.IsActive, ct);
            var projects = await _db.Projects.CountAsync(p => p.TenantId == tenant.Id && !p.IsDeleted, ct);
            var queries = await _db.Queries.CountAsync(q => q.TenantId == tenant.Id && !q.IsDeleted, ct);
            var products = await _db.Products.CountAsync(p => p.TenantId == tenant.Id && !p.IsDeleted, ct);

            rows.Add(new ReportRow(new Dictionary<string, object>
            {
                ["Tenant"] = tenant.Name,
                ["Plan"] = sub?.Plan?.PlanName ?? "None",
                ["Subscription Status"] = sub?.Status.ToString() ?? "None",
                ["Renewal Date"] = sub?.RenewalDate?.ToString("yyyy-MM-dd") ?? string.Empty,
                ["Users"] = users,
                ["Projects"] = projects,
                ["Queries"] = queries,
                ["Products"] = products
            }));
        }

        return rows;
    }
}
