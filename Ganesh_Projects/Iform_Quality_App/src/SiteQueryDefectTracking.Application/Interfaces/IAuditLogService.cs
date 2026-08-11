namespace SiteQueryDefectTracking.Application.Interfaces;

public record AuditLogEntry(
    string? UserId,
    string Action,
    string EntityName,
    string? EntityId,
    string? OldValue,
    string? NewValue,
    string? IpAddress,
    string? DeviceInfo);

public interface IAuditLogService
{
    Task RecordAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>Read query support for audit logs (Manager only).</summary>
public interface IAuditLogQueryService
{
    Task<Common.PagedResult<DTOs.Queries.AuditLogDto>> SearchAsync(
        DTOs.Audit.AuditLogSearchRequest request,
        CancellationToken cancellationToken = default);
}