namespace SiteQueryDefectTracking.Application.DTOs.Queries;

using SiteQueryDefectTracking.Domain.Enums;

public record CommentDto(Guid Id, Guid QueryId, string UserId, string UserName, string CommentText, DateTimeOffset CreatedAt);

public record StatusHistoryDto(Guid Id, QueryStatus FromStatus, QueryStatus ToStatus, string ChangedByUserId, string ChangedByName, DateTimeOffset ChangedAt, string? Reason);

public record QueryTimelineEntry(string Event, string? EntityId, DateTimeOffset At, string? Detail);

public record AttachmentDto(
    Guid Id,
    Guid QueryId,
    string OriginalFileName,
    string ContentType,
    long Size,
    int Width,
    int Height,
    string Type,
    DateTimeOffset UploadedAt,
    string UploadedBy,
    string? DownloadUrl);

public sealed record QueryDetailDto(
    Guid Id,
    string IPO,
    Guid ProjectId,
    string ProjectName,
    Guid IssueTypeId,
    string IssueTypeName,
    string? IssueTypeCode,
    QueryStatus Status,
    int QuantityNos,
    decimal? QuantitySqm,
    Guid? VerifiedProductCodeId,
    string? ProductCode,
    string? DispatchStatus,
    string RaisedByUserId,
    string RaisedByName,
    string? ResolvedByUserId,
    string? ResolvedByName,
    DateTimeOffset RaiseDate,
    DateTimeOffset? ResolvedDate,
    int DelayDays,
    string? SlabTarget,
    string? SlabCompleted,
    int? SlabDelayDays,
    int AttachmentCount,
    string? PreviewDescription)
    : QuerySummaryDto(Id, IPO, ProjectId, ProjectName, IssueTypeId, IssueTypeName, IssueTypeCode, Status, QuantityNos,
        QuantitySqm, VerifiedProductCodeId, ProductCode, DispatchStatus, RaisedByUserId, RaisedByName,
        ResolvedByUserId, ResolvedByName, RaiseDate, ResolvedDate, DelayDays, SlabTarget, SlabCompleted,
        SlabDelayDays, AttachmentCount, PreviewDescription)
{
    public string? Description { get; init; }
    public Guid? SlabId { get; init; }
    public IReadOnlyList<CommentDto> Comments { get; init; } = Array.Empty<CommentDto>();
    public IReadOnlyList<StatusHistoryDto> StatusHistory { get; init; } = Array.Empty<StatusHistoryDto>();
    public IReadOnlyList<AttachmentDto> Attachments { get; init; } = Array.Empty<AttachmentDto>();
    public IReadOnlyList<QueryTimelineEntry> Timeline { get; init; } = Array.Empty<QueryTimelineEntry>();
    public IReadOnlyList<EmailLogDto> Emails { get; init; } = Array.Empty<EmailLogDto>();
    public IReadOnlyList<AuditLogDto> AuditHistory { get; init; } = Array.Empty<AuditLogDto>();
}

public record EmailLogDto(
    Guid Id,
    Guid? QueryId,
    Guid? TemplateId,
    string? TemplateName,
    string Recipient,
    string Sender,
    string Subject,
    EmailLogStatus Status,
    DateTimeOffset? SentAt,
    string? ErrorMessage);

public record AuditLogDto(
    Guid Id,
    string? UserId,
    string? UserName,
    string Action,
    string EntityName,
    string? EntityId,
    string? OldValue,
    string? NewValue,
    DateTimeOffset Timestamp,
    string? IpAddress,
    string? DeviceInfo);