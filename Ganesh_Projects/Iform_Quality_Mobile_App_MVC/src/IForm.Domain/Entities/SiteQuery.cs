using IForm.Domain.Common;
using IForm.Domain.Enums;

namespace IForm.Domain.Entities;

/// <summary>
/// A site query / defect raised by a Site Engineer. Field names match the existing
/// Excel tracker exactly (BRD Section 7).
/// </summary>
public class SiteQuery : TenantEntity
{
    public string QueryNumber { get; set; } = string.Empty;
    public string IpoNumber { get; set; } = string.Empty;
    public Guid? IpoId { get; set; }
    public Ipo? Ipo { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public IssueType IssueType { get; set; }
    public decimal? QuantityNos { get; set; }
    public decimal? QuantitySqm { get; set; }
    public DispatchStatus DispatchStatus { get; set; } = DispatchStatus.Pending;
    public DateTime? SlabTargetCastingDate { get; set; }
    public DateTime? SlabCompletedDate { get; set; }
    public QueryStatus Status { get; set; } = QueryStatus.Pending;
    public string? StatusComment { get; set; }
    public Guid? AssignedToManagerId { get; set; }
    public ApplicationUser? AssignedToManager { get; set; }
    public string? Comments { get; set; }
    public DateTime RaisedDate { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedDate { get; set; }
    public Guid RaisedByUserId { get; set; }
    public ApplicationUser? RaisedByUser { get; set; }
    public string? Source { get; set; }

    /// <summary>Convenience display field carrying over the tracker's "Raised From" value.</summary>
    public string? RaisedFrom { get; set; }

    public ICollection<QueryPhoto> Photos { get; set; } = new List<QueryPhoto>();
    public ICollection<QueryComment> QueryComments { get; set; } = new List<QueryComment>();
    public ICollection<QueryStatusHistory> StatusHistory { get; set; } = new List<QueryStatusHistory>();
}

public class QueryPhoto : TenantEntity
{
    public Guid QueryId { get; set; }
    public SiteQuery? Query { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string ContentType { get; set; } = "image/jpeg";
    public long SizeBytes { get; set; }
    public string? Caption { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedByUserId { get; set; }
}

public class QueryComment : TenantEntity
{
    public Guid QueryId { get; set; }
    public SiteQuery? Query { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }
}

public class QueryStatusHistory : TenantEntity
{
    public Guid QueryId { get; set; }
    public SiteQuery? Query { get; set; }
    public QueryStatus OldStatus { get; set; }
    public QueryStatus NewStatus { get; set; }
    public Guid ChangedBy { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }
    public DateTime ChangedDateTime { get; set; } = DateTime.UtcNow;
    public string? Comments { get; set; }
}
