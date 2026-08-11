namespace SiteQueryDefectTracking.Application.DTOs.Notifications;

using SiteQueryDefectTracking.Domain.Enums;

public record NotificationDto(Guid Id, string Title, string? Message, NotificationType Type, bool IsRead, DateTimeOffset CreatedAt, string? EntityId);

public class MarkNotificationsReadRequest
{
    public IReadOnlyList<Guid>? Ids { get; set; }
}