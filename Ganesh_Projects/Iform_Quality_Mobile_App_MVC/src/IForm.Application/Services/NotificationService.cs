using IForm.Application.Common.Interfaces;
using IForm.Contracts;
using IForm.Domain.Entities;
using IForm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace IForm.Application.Services;

/// <summary>In-app notification service. Email notifications are dispatched by the
/// background notification processor using the same records.</summary>
public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _db;

    public NotificationService(IApplicationDbContext db) => _db = db;

    public async Task NotifyAsync(
        NotificationType type,
        string title,
        string message,
        Guid? userId = null,
        string? link = null,
        CancellationToken ct = default)
    {
        var tenantIds = new HashSet<Guid>();
        var targetUserIds = new List<Guid>();

        if (userId.HasValue)
        {
            var user = await _db.Users.FindAsync(new object?[] { userId.Value }, ct);
            if (user != null)
            {
                tenantIds.Add(user.TenantId);
                targetUserIds.Add(user.Id);
            }
        }
        else
        {
            var tenants = await _db.Tenants.Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.Trial)
                .Select(t => t.Id).ToListAsync(ct);
            tenantIds.UnionWith(tenants);
        }

        // Only target active users; honour the tenant filter for tenant-scoped notifications.
        var recipients = await _db.Users
            .Where(u => u.IsActive && tenantIds.Contains(u.TenantId))
            .Where(u => userId.HasValue ? u.Id == userId.Value : true)
            .Select(u => u.Id)
            .ToListAsync(ct);

        foreach (var recipientId in recipients)
        {
            var tenantId = await _db.Users.Where(u => u.Id == recipientId).Select(u => u.TenantId).FirstAsync(ct);
            _db.Notifications.Add(new Notification
            {
                TenantId = tenantId,
                UserId = recipientId,
                Type = type,
                Title = title,
                Message = message,
                Link = link,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Notification>> GetForCurrentUserAsync(Guid tenantId, Guid userId, int take = 50, CancellationToken ct = default) =>
        await _db.Notifications
            .Where(n => n.TenantId == tenantId && n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<int> GetUnreadCountAsync(Guid tenantId, Guid userId, CancellationToken ct = default) =>
        await _db.Notifications.CountAsync(n => n.TenantId == tenantId && n.UserId == userId && !n.IsRead, ct);

    public async Task MarkReadAsync(Guid id, Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId && x.UserId == userId, ct);
        if (n == null) return;
        n.IsRead = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var items = await _db.Notifications.Where(n => n.TenantId == tenantId && n.UserId == userId && !n.IsRead).ToListAsync(ct);
        foreach (var n in items) n.IsRead = true;
        if (items.Count > 0) await _db.SaveChangesAsync(ct);
    }
}

public class AuditLogger : IAuditLogger
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public AuditLogger(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task LogAsync(string action, string entityType, string? entityId = null, string? oldValue = null, string? newValue = null, CancellationToken ct = default)
    {
        var tenantId = _currentUser.TenantId;
        if (!tenantId.HasValue) return;

        _db.AuditLogs.Add(new AuditLog
        {
            TenantId = tenantId.Value,
            UserId = _currentUser.UserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OldValue = oldValue,
            NewValue = newValue,
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}
