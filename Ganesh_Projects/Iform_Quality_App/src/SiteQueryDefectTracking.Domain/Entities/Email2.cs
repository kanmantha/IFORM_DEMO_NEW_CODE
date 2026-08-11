using SiteQueryDefectTracking.Domain.Common;
using SiteQueryDefectTracking.Domain.Enums;

namespace SiteQueryDefectTracking.Domain.Entities;

public class EmailTemplate : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? IssueTypeId { get; set; }
    public IssueType? IssueType { get; set; }
    public string? DefaultRecipients { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}

public class EmailLog : BaseEntity
{
    public Guid? QueryId { get; set; }
    public Query? Query { get; set; }
    public Guid? TemplateId { get; set; }
    public EmailTemplate? Template { get; set; }
    public string Recipient { get; set; } = string.Empty;
    public string Sender { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public EmailStatus Status { get; set; } = EmailStatus.Generated;
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}