using SiteQueryDefectTracking.Domain.Common;

namespace SiteQueryDefectTracking.Domain.Entities;

public class Project : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ClientName { get; set; }
    public string? Location { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Query> Queries { get; set; } = new List<Query>();
    public ICollection<ProductProjectMapping> ProductMappings { get; set; } = new List<ProductProjectMapping>();
}

public class IssueType : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<Query> Queries { get; set; } = new List<Query>();
}