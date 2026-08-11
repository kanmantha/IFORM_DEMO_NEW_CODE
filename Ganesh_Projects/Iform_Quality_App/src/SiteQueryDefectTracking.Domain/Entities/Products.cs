using SiteQueryDefectTracking.Domain.Common;

namespace SiteQueryDefectTracking.Domain.Entities;

public class ProductCode : AuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public string? Material { get; set; }
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public string? Barcode { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsVerified { get; set; }
    public DateTimeOffset? LastImportedAt { get; set; }

    public ICollection<ProductSpecification> Specifications { get; set; } = new List<ProductSpecification>();
    public ICollection<ProductProjectMapping> ProjectMappings { get; set; } = new List<ProductProjectMapping>();
}

public class ProductSpecification : BaseEntity
{
    public Guid ProductCodeId { get; set; }
    public ProductCode? ProductCode { get; set; }
    public string AttributeName { get; set; } = string.Empty;
    public string AttributeValue { get; set; } = string.Empty;
}

public class ProductProjectMapping : BaseEntity
{
    public Guid ProductCodeId { get; set; }
    public ProductCode? ProductCode { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}