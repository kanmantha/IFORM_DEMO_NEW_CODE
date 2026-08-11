namespace SiteQueryDefectTracking.Application.DTOs.Products;

using SiteQueryDefectTracking.Application.Common;

public record ProductSummaryDto(
    Guid Id,
    string Code,
    string Description,
    string? Category,
    string? Unit,
    string? Barcode,
    bool IsActive,
    DateTimeOffset? LastImportedAt)
{
    public int ProjectMappingCount { get; init; }
}

public sealed record ProductDetailDto(
    Guid Id, string Code, string Description, string? Category, string? Unit,
    string? Barcode, bool IsActive, DateTimeOffset? LastImportedAt)
    : ProductSummaryDto(Id, Code, Description, Category, Unit, Barcode, IsActive, LastImportedAt)
{
    public IReadOnlyList<ProductSpecificationDto> Specifications { get; init; } = Array.Empty<ProductSpecificationDto>();
    public IReadOnlyList<ProductProjectMappingDto> ProjectMappings { get; init; } = Array.Empty<ProductProjectMappingDto>();
}

public record ProductSpecificationDto(string AttributeName, string AttributeValue);

public record ProductProjectMappingDto(Guid ProjectId, string ProjectName, bool IsActive);

public class ProductSearchRequest
{
    public int Page { get; set; } = Pagination.DefaultPage;
    public int PageSize { get; set; } = Pagination.DefaultPageSize;
    public string? Query { get; set; }
    public string? Category { get; set; }
    public Guid? ProjectId { get; set; }
}

public class CreateProductRequest
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Unit { get; set; }
    public string? Barcode { get; set; }
    public IReadOnlyList<ProductSpecificationDto> Specifications { get; set; } = Array.Empty<ProductSpecificationDto>();
    public IReadOnlyList<Guid> ProjectIds { get; set; } = Array.Empty<Guid>();
}

public class UpdateProductRequest : CreateProductRequest
{
    public bool IsActive { get; set; } = true;
}

public record ProductImportRow(
    string Code,
    string Description,
    string? Category,
    string? Unit,
    string? Barcode,
    IReadOnlyList<ProductSpecificationDto> Specifications);

public record ProductImportSummary(
    string JobId,
    int TotalRows,
    int ValidRows,
    int InvalidRows,
    int Created,
    int Updated,
    IReadOnlyList<string> Errors);

public record ImportStatusDto(
    string JobId,
    bool IsComplete,
    int TotalRows,
    int ProcessedRows,
    int Created,
    int Updated,
    int InvalidRows,
    IReadOnlyList<string> Errors,
    string? State);