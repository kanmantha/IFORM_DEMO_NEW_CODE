namespace SiteQueryDefectTracking.Application.DTOs.Queries;

using SiteQueryDefectTracking.Domain.Enums;

public record QuerySummaryDto(
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
{
    public bool IsSlaBreached { get; init; }
    public bool IsPublic { get; init; }
}