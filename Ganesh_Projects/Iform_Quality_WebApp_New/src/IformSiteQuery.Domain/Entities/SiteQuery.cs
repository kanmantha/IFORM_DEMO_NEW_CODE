using IformSiteQuery.Domain.Enums;

namespace IformSiteQuery.Domain.Entities;

public class SiteQuery
{
    public int Id { get; set; }
    public string QueryNumber { get; set; } = string.Empty;
    public string IpoNumber { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public IssueType IssueType { get; set; }
    public decimal QtyNos { get; set; }
    public decimal QtySqm { get; set; }
    public string? Description { get; set; }
    public string? PhotoPath { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public QueryStatus Status { get; set; } = QueryStatus.Pending;
    public DateTime? SlabTargetDate { get; set; }
    public DateTime? SlabCompletedDate { get; set; }
    public int RaisedById { get; set; }
    public User? RaisedBy { get; set; }
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
    public int? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }

    public int SlabDelayDays =>
        (SlabCompletedDate ?? SlabTargetDate).HasValue
            ? Math.Max(0, (int)((SlabCompletedDate ?? SlabTargetDate)!.Value.Date - (SlabTargetDate!.Value.Date)).TotalDays)
            : 0;

    public int DelayDays =>
        Status == QueryStatus.Resolved && ResolvedAt.HasValue
            ? Math.Max(0, (int)(ResolvedAt.Value.Date - RaisedAt.Date).TotalDays)
            : Math.Max(0, (int)(DateTime.UtcNow.Date - RaisedAt.Date).TotalDays);
}
