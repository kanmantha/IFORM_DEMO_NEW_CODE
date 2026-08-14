using IForm.Domain.Common;

namespace IForm.Domain.Entities;

public class ProductCategory : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Product : TenantEntity
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public string? Material { get; set; }
    public string? Unit { get; set; }
    public Guid? CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public string? PhotoPath { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Source { get; set; }

    public ICollection<ProductProjectMapping> ProjectMappings { get; set; } = new List<ProductProjectMapping>();
    public ICollection<SiteQuery> Queries { get; set; } = new List<SiteQuery>();
}

public class ProductProjectMapping : TenantEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
}
