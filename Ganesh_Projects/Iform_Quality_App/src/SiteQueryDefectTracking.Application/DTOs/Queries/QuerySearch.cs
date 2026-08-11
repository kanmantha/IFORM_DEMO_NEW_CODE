namespace SiteQueryDefectTracking.Application.DTOs.Queries;

using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Domain.Enums;

public class QuerySearchRequest
{
    public int Page { get; set; } = Pagination.DefaultPage;
    public int PageSize { get; set; } = Pagination.DefaultPageSize;
    public string? SortBy { get; set; } = "DelayDays";
    public string? SortDirection { get; set; } = "desc";

    public string? IPO { get; set; }
    public string? Keyword { get; set; }
    public Guid? ProjectId { get; set; }
    public Guid? IssueTypeId { get; set; }
    public QueryStatus? Status { get; set; }
    public DateTimeOffset? DateFrom { get; set; }
    public DateTimeOffset? DateTo { get; set; }
    public string? RaisedByUserId { get; set; }
    public bool? MineOnly { get; set; }
}

public class AddCommentRequest
{
    public string CommentText { get; set; } = string.Empty;
}