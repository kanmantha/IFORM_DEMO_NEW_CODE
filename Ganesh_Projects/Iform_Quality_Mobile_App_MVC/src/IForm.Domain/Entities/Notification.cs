using IForm.Domain.Common;
using IForm.Domain.Enums;

namespace IForm.Domain.Entities;

public class Notification : TenantEntity
{
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Link { get; set; }
    public bool IsRead { get; set; }
    public bool EmailSent { get; set; }
}

public class AuditLog : TenantEntity
{
    public Guid? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TenantSlug { get; set; }
}
