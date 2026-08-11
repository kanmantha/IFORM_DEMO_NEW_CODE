using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.DTOs.Notifications;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Entities;

namespace SiteQueryDefectTracking.Application.Services;

public class NotificationService(
    IApplicationDbContext context,
    ICurrentUserService currentUser) : INotificationService
{
    public async Task<IReadOnlyList<NotificationDto>> GetMineAsync(CancellationToken ct = default)
    {
        var user = currentUser.UserId!;
        return await context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == user)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .Select(n => new NotificationDto(
                n.Id, n.Title, n.Body, n.Type, n.IsRead, n.CreatedAt,
                n.EntityId.HasValue ? n.EntityId.Value.ToString() : null))
            .ToListAsync(ct);
    }

    public async Task MarkReadAsync(MarkNotificationsReadRequest request, CancellationToken ct = default)
    {
        var user = currentUser.UserId!;
        var query = context.Notifications.Where(n => n.UserId == user && !n.IsRead);
        if (request.Ids is { Count: > 0 })
        {
            query = query.Where(n => request.Ids.Contains(n.Id));
        }

        var items = await query.ToListAsync(ct);
        foreach (var item in items)
        {
            item.IsRead = true;
        }

        await context.SaveChangesAsync(ct);
    }
}

/// <summary>Creates user-facing notifications raised from domain events.</summary>
public class NotificationWriter(IApplicationDbContext context)
{
    public async Task NotifyManagersAsync(string title, string body, string type, Guid? entityId, CancellationToken ct = default)
    {
        var managers = await context.Users
            .Where(u => u.IsActive)
            .ToListAsync(ct);

        foreach (var manager in managers)
        {
            context.Notifications.Add(new Notification
            {
                UserId = manager.Id,
                Title = title,
                Body = body,
                Type = ParseType(type),
                EntityId = entityId
            });
        }

        await context.SaveChangesAsync(ct);
    }

    private static Domain.Enums.NotificationType ParseType(string type) =>
        Enum.TryParse<Domain.Enums.NotificationType>(type, true, out var parsed)
            ? parsed
            : Domain.Enums.NotificationType.Info;
}