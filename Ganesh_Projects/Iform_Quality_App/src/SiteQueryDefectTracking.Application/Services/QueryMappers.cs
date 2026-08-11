namespace SiteQueryDefectTracking.Application.Services;

using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Domain.Entities;

public static class QueryMappers
{
    public static QuerySummaryDto ToSummary(Query q)
    {
        var productCode = q.VerifiedProductCode?.Code ?? q.ProductCodeText ?? string.Empty;
        return new QuerySummaryDto(
            Id: q.Id,
            IPO: q.IPO,
            ProjectId: q.ProjectId,
            ProjectName: q.Project?.Name ?? string.Empty,
            IssueTypeId: q.IssueTypeId,
            IssueTypeName: q.IssueType?.Name ?? string.Empty,
            IssueTypeCode: q.IssueType?.Code,
            Status: q.Status,
            QuantityNos: q.QuantityNos ?? 0,
            QuantitySqm: q.QuantitySqm,
            VerifiedProductCodeId: q.VerifiedProductCodeId,
            ProductCode: productCode,
            DispatchStatus: q.DispatchStatus.ToString(),
            RaisedByUserId: q.RaisedByUserId,
            RaisedByName: q.RaisedByUser?.FullName ?? q.RaisedByUser?.UserName ?? string.Empty,
            ResolvedByUserId: q.ResolvedByUserId,
            ResolvedByName: q.ResolvedByUser?.FullName ?? q.ResolvedByUser?.UserName,
            RaiseDate: q.RaiseDate,
            ResolvedDate: q.ResolvedDate,
            DelayDays: q.DelayDays,
            SlabTarget: q.SlabTarget,
            SlabCompleted: q.SlabCompleted,
            SlabDelayDays: q.SlabDelayDays,
            AttachmentCount: q.Attachments?.Count ?? 0,
            PreviewDescription: q.Description)
        {
            IsSlaBreached = q.Status != Domain.Enums.QueryStatus.Resolved && q.DelayDays > 0
        };
    }

    public static CommentDto ToComment(QueryComment c) =>
        new(c.Id, c.QueryId, c.UserId, c.User?.FullName ?? c.User?.UserName ?? string.Empty, c.CommentText, c.CreatedAt);

    public static StatusHistoryDto ToStatusHistory(QueryStatusHistory h) =>
        new(h.Id, h.FromStatus, h.ToStatus, h.ChangedByUserId, h.ChangedByUser?.FullName ?? h.ChangedByUser?.UserName ?? string.Empty, h.ChangedAt, h.Reason);

    public static AuditLogDto ToAudit(AuditLog a) =>
        new(a.Id, a.UserId, a.Username, a.Action, a.EntityName, a.EntityId, a.OldValue, a.NewValue, a.Timestamp, a.IpAddress, a.DeviceInfo);
}