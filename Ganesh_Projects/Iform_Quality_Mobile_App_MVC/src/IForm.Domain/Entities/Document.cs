using IForm.Domain.Common;
using IForm.Domain.Enums;

namespace IForm.Domain.Entities;

public class Document : TenantEntity
{
    public string Title { get; set; } = string.Empty;
    public DocumentCategory Category { get; set; } = DocumentCategory.Other;
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }
    public Guid? QueryId { get; set; }
    public SiteQuery? Query { get; set; }
    public Guid? EotId { get; set; }
    public EotRecord? Eot { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Description { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedByUserId { get; set; }
    public ApplicationUser? UploadedByUser { get; set; }
}
