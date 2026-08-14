using System.ComponentModel.DataAnnotations;
using IForm.Domain.Enums;

namespace IForm.Application.DTOs;

public class CreateQueryRequest
{
    public string? QueryNumber { get; set; }
    [Required] public string IpoNumber { get; set; } = string.Empty;
    public Guid? IpoId { get; set; }
    [Required] public Guid ProjectId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    [Required] public IssueType IssueType { get; set; }
    public decimal? QuantityNos { get; set; }
    public decimal? QuantitySqm { get; set; }
    public DispatchStatus DispatchStatus { get; set; } = DispatchStatus.Pending;
    public DateTime? SlabTargetCastingDate { get; set; }
    public DateTime? SlabCompletedDate { get; set; }
    public string? Comments { get; set; }
    public string? RaisedFrom { get; set; }
}

public class UpdateQueryRequest
{
    public string? IpoNumber { get; set; }
    public Guid? IpoId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? ProductId { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public IssueType IssueType { get; set; }
    public decimal? QuantityNos { get; set; }
    public decimal? QuantitySqm { get; set; }
    public DispatchStatus DispatchStatus { get; set; }
    public DateTime? SlabTargetCastingDate { get; set; }
    public DateTime? SlabCompletedDate { get; set; }
    public string? Comments { get; set; }
}

public record QuerySearchRequest(
    string? SearchTerm = null,
    Guid? ProjectId = null,
    Guid? IpoId = null,
    Guid? ProductId = null,
    IssueType? IssueType = null,
    QueryStatus? Status = null,
    Guid? RaisedById = null,
    int? MinDelayDays = null,
    int? MaxDelayDays = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null,
    bool MyQueries = false,
    string? SortBy = "delay",
    bool SortDescending = true,
    int Page = 1,
    int PageSize = 20);

public record QueryListItemDto(
    Guid Id, string QueryNumber, string IpoNumber, string ProjectName, string? ProductCode, string? ProductName,
    IssueType IssueType, decimal? QuantityNos, decimal? QuantitySqm, DispatchStatus DispatchStatus,
    QueryStatus Status, int DelayDays, SeverityLevel Severity, string RaisedByName, DateTime RaisedDate, DateTime? ResolvedDate);

public record QueryDetailDto(
    Guid Id, string QueryNumber, string IpoNumber, Guid? IpoId, Guid ProjectId, string ProjectName,
    Guid? ProductId, string? ProductCode, string? ProductName, string? ProductDescription, string? ProductSpecification,
    IssueType IssueType, decimal? QuantityNos, decimal? QuantitySqm, DispatchStatus DispatchStatus,
    DateTime? SlabTargetCastingDate, DateTime? SlabCompletedDate, int? SlabDelayDays,
    QueryStatus Status, string? StatusComment, int DelayDays, SeverityLevel Severity, string? Comments,
    Guid RaisedByUserId, string RaisedByName, DateTime RaisedDate, DateTime? ResolvedDate,
    IReadOnlyList<PhotoDto> Photos, IReadOnlyList<CommentDto> CommentsList, IReadOnlyList<StatusHistoryDto> StatusHistory, IReadOnlyList<AuditDto> AuditEntries);

public record PhotoDto(Guid Id, string FilePath, string FileName, string ContentType, long SizeBytes, DateTime UploadedAt, string? Url);

public record CommentDto(Guid Id, string Body, string AuthorName, DateTime CreatedAt);

public record StatusHistoryDto(QueryStatus OldStatus, QueryStatus NewStatus, string ChangedByName, DateTime ChangedDateTime, string? Comments);

public record AuditDto(Guid Id, string Action, string EntityType, string? EntityId, string? OldValue, string? NewValue, string? UserName, DateTime Timestamp);

public record AddCommentRequest(string Body);

public record ChangeStatusRequest(QueryStatus NewStatus, string? Comment = null);
