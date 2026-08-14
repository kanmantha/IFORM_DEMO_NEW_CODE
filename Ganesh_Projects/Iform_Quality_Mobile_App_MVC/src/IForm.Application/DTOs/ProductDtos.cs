using System.ComponentModel.DataAnnotations;
using IForm.Domain.Enums;

namespace IForm.Application.DTOs;

public record ProductDto(Guid Id, string ProductCode, string ProductName, string? Description, string? Specification, string? Material, string? Unit, Guid? CategoryId, string? CategoryName, string? PhotoPath, bool IsActive, IReadOnlyList<Guid> ProjectIds);

public record ProductListItemDto(Guid Id, string ProductCode, string ProductName, string? CategoryName, string? Specification, string? Material, string? Unit, bool IsActive, string? PhotoPath);

public class CreateProductRequest
{
    [Required] public string ProductCode { get; set; } = string.Empty;
    [Required] public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Specification { get; set; }
    public string? Material { get; set; }
    public string? Unit { get; set; }
    public Guid? CategoryId { get; set; }
    public bool IsActive { get; set; } = true;
    public IReadOnlyList<Guid> ProjectIds { get; set; } = Array.Empty<Guid>();
}

public class UpdateProductRequest : CreateProductRequest { }

public record ProductImportResult(int TotalRecords, int Imported, int Duplicates, int Invalid, int Warnings, int Errors, IReadOnlyList<string> Messages, string? ErrorReportFile);

public record ProductCategoryDto(Guid Id, string Name, string? Description, int ProductCount);
