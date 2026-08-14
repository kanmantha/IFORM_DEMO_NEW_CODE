using IForm.Domain.Common;

namespace IForm.Domain.Entities;

/// <summary>
/// Auto-generated email template. One default template per issue type
/// (Missing, Production Mistake, Design Mistake, Dispatch Missing).
/// </summary>
public class EmailTemplate : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? IssueType { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public string? ToRecipients { get; set; }
    public string? CcRecipients { get; set; }
    public string? BccRecipients { get; set; }
}

public class EmailRecord : TenantEntity
{
    public string TemplateName { get; set; } = string.Empty;
    public Guid? QueryId { get; set; }
    public SiteQuery? Query { get; set; }
    public string To { get; set; } = string.Empty;
    public string Cc { get; set; } = string.Empty;
    public string Bcc { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsHtml { get; set; }
    public bool IsDraft { get; set; }
    public bool Sent { get; set; }
    public DateTime? SentAt { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }
}
