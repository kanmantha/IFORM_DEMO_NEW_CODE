using IForm.Application.Common.Interfaces;
using IForm.Domain.Enums;
using IForm.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IForm.Application.Services;

public interface IEscalationService
{
    /// <summary>
    /// Finds open queries whose delay has reached the tenant's escalation threshold
    /// and notifies the configured role (default: Manager). Each query is escalated
    /// only once (deduplicated via the existing CriticalDelay notification link).
    /// </summary>
    Task<int> ProcessEscalationsAsync(CancellationToken ct = default);
}

public class EscalationService : IEscalationService
{
    private readonly IApplicationDbContext _db;
    private readonly ITenantSettingsProvider _settings;
    private readonly INotificationService _notifications;
    private readonly ILogger<EscalationService> _logger;

    public EscalationService(
        IApplicationDbContext db,
        ITenantSettingsProvider settings,
        INotificationService notifications,
        ILogger<EscalationService> logger)
    {
        _db = db;
        _settings = settings;
        _notifications = notifications;
        _logger = logger;
    }

    public async Task<int> ProcessEscalationsAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var escalated = 0;

        var tenants = await _db.Tenants
            .Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial)
            .Select(t => t.Id)
            .ToListAsync(ct);

        foreach (var tenantId in tenants)
        {
            var features = _settings.GetFeatures(tenantId);
            if (!features.EscalationEnabled) continue;

            var queries = await _db.Queries.IgnoreQueryFilters()
                .Where(q => q.TenantId == tenantId && !q.IsDeleted && q.Status != QueryStatus.Resolved)
                .Select(q => new { q.Id, q.QueryNumber, q.IpoNumber, q.RaisedDate, q.ResolvedDate })
                .ToListAsync(ct);

            foreach (var query in queries)
            {
                var delay = QueryBusinessRules.CalculateDelayDays(query.RaisedDate, query.ResolvedDate, today);
                if (delay < features.EscalationDays) continue;

                var link = $"/Queries/Details/{query.Id}";
                var alreadyNotified = await _db.Notifications.IgnoreQueryFilters()
                    .AnyAsync(n => n.TenantId == tenantId && n.Type == NotificationType.CriticalDelay && n.Link == link, ct);
                if (alreadyNotified) continue;

                var recipients = await (
                    from ur in _db.UserRoles
                    join r in _db.Roles on ur.RoleId equals r.Id
                    join u in _db.Users on ur.UserId equals u.Id
                    where r.Name == features.EscalationRole && u.TenantId == tenantId && u.IsActive
                    select u.Id).ToListAsync(ct);

                if (recipients.Count == 0)
                {
                    _logger.LogWarning("Escalation configured for tenant {TenantId} but no active users in role {Role}; skipping {Query}.",
                        tenantId, features.EscalationRole, query.QueryNumber);
                    continue;
                }

                foreach (var recipientId in recipients)
                {
                    await _notifications.NotifyAsync(
                        NotificationType.CriticalDelay,
                        "Query escalated",
                        $"Query {query.QueryNumber} ({query.IpoNumber}) has been open for {delay} days and needs attention.",
                        userId: recipientId,
                        link: link,
                        ct: ct);
                }

                escalated++;
                _logger.LogInformation("Escalated query {Query} (tenant {TenantId}, {Delay} days).", query.QueryNumber, tenantId, delay);
            }
        }

        return escalated;
    }
}
