namespace IformSiteQuery.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Spec { get; set; }
    public string? Material { get; set; }
    public bool IsActive { get; set; } = true;
}
