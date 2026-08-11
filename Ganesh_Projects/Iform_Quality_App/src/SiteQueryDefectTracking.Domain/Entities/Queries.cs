using SiteQueryDefectTracking.Domain.Common;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Domain.Entities;

public class Query : AuditableEntity
{
    public string QueryNo { get; set; } = string.Empty;
    public string IPO { get; set; } = string.Empty;

    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid IssueTypeId { get; set; }
    public IssueType? IssueType { get; set; }

    /// <summary>Verified product code lookup (BRD Module 4). FKs to ProductCode.</summary>
    public Guid? VerifiedProductCodeId { get; set; }
    public ProductCode? VerifiedProductCode { get; set; }

    /// <summary>Free-text convenience field; identity is governed by VerifiedProductCodeId.</summary>
    public string? ProductCodeText { get; set; }

    public int? QuantityNos { get; set; }
    public decimal? QuantitySqm { get; set; }

    public DispatchStatus DispatchStatus { get; set; } = DispatchStatus.NotDispatched;
    public string? DispatchRemark { get; set; }

    public QueryStatus Status { get; set; } = QueryStatus.Pending;

    public DateTimeOffset RaiseDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedDate { get; set; }

    public string? Description { get; set; }

    public string RaisedByUserId { get; set; } = string.Empty;
    public User? RaisedByUser { get; set; }

    public string? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }

    public string? SlabTarget { get; set; }
    public string? SlabCompleted { get; set; }
    public int? SlabDelayDays { get; set; }

    /// <summary>Application/domain calculated; never edited directly by clients.</summary>
    public int DelayDays { get; set; }

    public bool IsOpen => Status != QueryStatus.Resolved;

    public ICollection<QueryComment> Comments { get; set; } = new List<QueryComment>();
    public ICollection<Attachment> Attachments { get; set; } = new List<Attachment>();
    public ICollection<QueryStatusHistory> StatusHistory { get; set; } = new List<QueryStatusHistory>();
    public ICollection<EmailLog> Emails { get; set; } = new List<EmailLog>();
}

public class QueryComment : BaseEntity
{
    public Guid QueryId { get; set; }
    public Query? Query { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    public string CommentText { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class QueryStatusHistory : BaseEntity
{
    public Guid QueryId { get; set; }
    public Query? Query { get; set; }
    public QueryStatus FromStatus { get; set; }
    public QueryStatus ToStatus { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public User? ChangedByUser { get; set; }
    public DateTimeOffset ChangedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Reason { get; set; }
}

public class Attachment : BaseEntity
{
    public Guid QueryId { get; set; }
    public Query? Query { get; set; }

    public string UploadedByUserId { get; set; } = string.Empty;
    public User? UploadedByUser { get; set; }

    public AttachmentType Type { get; set; } = AttachmentType.Photo;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Size { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RetentionExpiry { get; set; }
}