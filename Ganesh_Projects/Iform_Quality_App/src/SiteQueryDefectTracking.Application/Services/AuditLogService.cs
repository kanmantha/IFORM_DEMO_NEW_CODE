using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Audit;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Entities;

namespace SiteQueryDefectTracking.Application.Services;

public class AuditLogService(IApplicationDbContext context) : IAuditLogService, IAuditLogQueryService
{
    public Task RecordAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        context.AuditLogs.Add(new AuditLog
        {
            UserId = entry.UserId,
            Username = null,
            Action = entry.Action,
            EntityName = entry.EntityName,
            EntityId = entry.EntityId,
            OldValue = entry.OldValue,
            NewValue = entry.NewValue,
            IpAddress = entry.IpAddress,
            DeviceInfo = entry.DeviceInfo
        });
        return context.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogDto>> SearchAsync(AuditLogSearchRequest request, CancellationToken ct = default)
    {
        IQueryable<AuditLog> query = context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(a => a.Action == request.Action);
        if (!string.IsNullOrWhiteSpace(request.EntityName)) query = query.Where(a => a.EntityName == request.EntityName);
        if (!string.IsNullOrWhiteSpace(request.EntityId)) query = query.Where(a => a.EntityId == request.EntityId);
        if (!string.IsNullOrEmpty(request.UserId)) query = query.Where(a => a.UserId == request.UserId);
        if (request.From.HasValue) query = query.Where(a => a.Timestamp >= request.From.Value);
        if (request.To.HasValue) query = query.Where(a => a.Timestamp <= request.To.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.UserId, a.Username, a.Action, a.EntityName, a.EntityId,
                a.OldValue, a.NewValue, a.Timestamp, a.IpAddress, a.DeviceInfo))
            .ToListAsync(ct);

        return PagedResult<AuditLogDto>.Create(items, total, request.Page, request.PageSize);
    }
}