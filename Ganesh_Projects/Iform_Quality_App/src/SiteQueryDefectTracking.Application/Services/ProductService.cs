using Microsoft.EntityFrameworkCore;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Products;
using SiteQueryDefectTracking.Application.Exceptions;
using SiteQueryDefectTracking.Application.Interfaces;
using SiteQueryDefectTracking.Domain.Constants;
using SiteQueryDefectTracking.Domain.Entities;

namespace SiteQueryDefectTracking.Application.Services;

public class ProductService(
    IApplicationDbContext context,
    IAuditLogService auditLog,
    ICurrentUserService currentUser,
    ICatalogueJobStore jobs) : IProductService
{
    public async Task<PagedResult<ProductSummaryDto>> SearchAsync(ProductSearchRequest request, CancellationToken ct = default)
    {
        IQueryable<ProductCode> query = context.ProductCodes.AsNoTracking().Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLowerInvariant();
            query = query.Where(p =>
                p.Code.ToLower().Contains(term)
                || (p.Name != null && p.Name.ToLower().Contains(term))
                || (p.Description != null && p.Description.ToLower().Contains(term))
                || (p.Barcode != null && p.Barcode.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(request.Category))
            query = query.Where(p => p.Category == request.Category);

        if (request.ProjectId.HasValue)
        {
            var projectId = request.ProjectId.Value;
            query = query.Where(p => p.ProjectMappings.Any(m => m.ProjectId == projectId && m.IsActive));
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .AsNoTracking()
            .Include(p => p.ProjectMappings)
            .OrderBy(p => p.Code)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        var dtos = items.Select(ToSummary).ToList();
        return PagedResult<ProductSummaryDto>.Create(dtos, total, request.Page, request.PageSize);
    }

    public async Task<ProductDetailDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var product = await context.ProductCodes
            .AsNoTracking()
            .Include(p => p.Specifications)
            .Include(p => p.ProjectMappings).ThenInclude(m => m.Project)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product code", id);

        return ToDetail(product);
    }

    public async Task<ProductDetailDto> CreateAsync(CreateProductRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        if (await context.ProductCodes.AnyAsync(p => p.Code == request.Code.Trim(), ct))
            throw new BusinessException($"A product with code '{request.Code}' already exists.");

        var product = new ProductCode
        {
            Code = request.Code.Trim(),
            Name = request.Description.Trim(),
            Description = request.Description.Trim(),
            Category = request.Category,
            Unit = request.Unit,
            Barcode = request.Barcode?.Trim(),
            IsActive = true,
            IsVerified = true,
            LastImportedAt = DateTimeOffset.UtcNow
        };

        ApplySpecifications(product, request.Specifications);
        ApplyProjectMappings(product, request.ProjectIds);

        context.ProductCodes.Add(product);
        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.ProductModified, nameof(ProductCode), product.Id.ToString(),
            null, Json(request), ipAddress, deviceInfo), ct);

        return await GetAsync(product.Id, ct);
    }

    public async Task<ProductDetailDto> UpdateAsync(Guid id, UpdateProductRequest request, string? ipAddress, string? deviceInfo, CancellationToken ct = default)
    {
        var product = await context.ProductCodes
            .Include(p => p.Specifications)
            .Include(p => p.ProjectMappings)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("Product code", id);

        product.Code = request.Code.Trim();
        product.Name = request.Description.Trim();
        product.Description = request.Description.Trim();
        product.Category = request.Category;
        product.Unit = request.Unit;
        product.Barcode = request.Barcode?.Trim();
        product.IsActive = request.IsActive;

        context.ProductSpecifications.RemoveRange(product.Specifications);
        product.Specifications = new List<ProductSpecification>();
        ApplySpecifications(product, request.Specifications);

        context.ProductProjectMappings.RemoveRange(product.ProjectMappings);
        product.ProjectMappings.Clear();
        ApplyProjectMappings(product, request.ProjectIds);

        await context.SaveChangesAsync(ct);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.ProductModified, nameof(ProductCode), id.ToString(),
            null, Json(request), ipAddress, deviceInfo), ct);

        return await GetAsync(id, ct);
    }

    public Task<ProductImportSummary> ImportPreviewAsync(IReadOnlyList<ProductImportRow> rows, CancellationToken ct = default)
    {
        var errors = new List<string>();
        var valid = new List<ProductImportRow>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Code))
            {
                errors.Add("Row with empty code rejected.");
                continue;
            }

            var code = row.Code.Trim();
            if (!seen.Add(code))
            {
                errors.Add($"Duplicate code '{code}' ignored.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(row.Description))
            {
                errors.Add($"Row '{code}' missing description; rejected.");
                continue;
            }

            valid.Add(row);
        }

        return Task.FromResult(new ProductImportSummary(
            JobId: jobs.Create(valid, errors),
            TotalRows: rows.Count,
            ValidRows: valid.Count,
            InvalidRows: rows.Count - valid.Count,
            Created: 0,
            Updated: 0,
            Errors: errors));
    }

    public async Task<ProductImportSummary> CommitImportAsync(string jobId, CancellationToken ct = default)
    {
        var job = jobs.Get(jobId) ?? throw new NotFoundException("Import job", jobId);

        var created = 0;
        var updated = 0;

        foreach (var row in job.Rows)
        {
            var code = row.Code.Trim();
            var existing = await context.ProductCodes.FirstOrDefaultAsync(p => p.Code == code, ct);
            if (existing is null)
            {
                context.ProductCodes.Add(new ProductCode
                {
                    Code = code,
                    Name = row.Description.Trim(),
                    Description = row.Description.Trim(),
                    Category = row.Category,
                    Unit = row.Unit,
                    Barcode = row.Barcode?.Trim(),
                    IsActive = true,
                    IsVerified = true,
                    LastImportedAt = DateTimeOffset.UtcNow
                });
                created++;
            }
            else
            {
                existing.Name = row.Description.Trim();
                existing.Description = row.Description.Trim();
                existing.Category = row.Category;
                existing.Unit = row.Unit;
                existing.Barcode = row.Barcode?.Trim();
                existing.IsVerified = true;
                existing.LastImportedAt = DateTimeOffset.UtcNow;
                updated++;
            }
        }

        await context.SaveChangesAsync(ct);
        jobs.MarkCommitted(jobId, created, updated);

        await auditLog.RecordAsync(new AuditLogEntry(
            currentUser.UserId, AuditActions.CatalogueUploaded, nameof(ProductCode), null,
            null, $"Created {created}, Updated {updated}.", currentUser.IpAddress, currentUser.DeviceInfo), ct);

        return new ProductImportSummary(jobId, job.Rows.Count, job.Rows.Count, 0, created, updated, Array.Empty<string>());
    }

    public Task<ImportStatusDto?> GetImportStatusAsync(string jobId, CancellationToken ct = default)
    {
        var job = jobs.Get(jobId);
        if (job is null) return Task.FromResult<ImportStatusDto?>(null);

        return Task.FromResult<ImportStatusDto?>(new ImportStatusDto(
            job.JobId, true, job.Rows.Count, job.Rows.Count, job.Created, job.Updated, 0, job.Errors, "Completed"));
    }

    private static ProductSummaryDto ToSummary(ProductCode p)
    {
        return new ProductSummaryDto(
            p.Id, p.Code, p.Description ?? string.Empty, p.Category, p.Unit, p.Barcode,
            p.IsActive, p.LastImportedAt)
        {
            ProjectMappingCount = p.ProjectMappings.Count(m => m.IsActive)
        };
    }

    private static ProductDetailDto ToDetail(ProductCode p)
    {
        var summary = ToSummary(p);
        return new ProductDetailDto(p.Id, p.Code, p.Description ?? string.Empty, p.Category, p.Unit,
            p.Barcode, p.IsActive, p.LastImportedAt)
        {
            Specifications = p.Specifications
                .Select(s => new ProductSpecificationDto(s.AttributeName, s.AttributeValue)).ToList(),
            ProjectMappings = p.ProjectMappings
                .Where(m => m.IsActive)
                .Select(m => new ProductProjectMappingDto(m.ProjectId, m.Project?.Name ?? string.Empty, m.IsActive)).ToList()
        };
    }

    private static void ApplySpecifications(ProductCode product, IReadOnlyList<ProductSpecificationDto> specs)
    {
        foreach (var spec in specs)
        {
            if (string.IsNullOrWhiteSpace(spec.AttributeName)) continue;
            product.Specifications.Add(new ProductSpecification
            {
                AttributeName = spec.AttributeName.Trim(),
                AttributeValue = spec.AttributeValue?.Trim() ?? string.Empty
            });
        }
    }

    private void ApplyProjectMappings(ProductCode product, IReadOnlyList<Guid> projectIds)
    {
        foreach (var projectId in projectIds)
        {
            product.ProjectMappings.Add(new ProductProjectMapping
            {
                ProjectId = projectId,
                IsActive = true
            });
        }
    }

    private static string Json(object value) => System.Text.Json.JsonSerializer.Serialize(value);
}

public class ImportJob
{
    public string JobId { get; init; } = string.Empty;
    public IReadOnlyList<ProductImportRow> Rows { get; init; } = Array.Empty<ProductImportRow>();
    public int Created { get; set; }
    public int Updated { get; set; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public string State { get; init; } = "Preview";
}

public interface ICatalogueJobStore
{
    string Create(IReadOnlyList<ProductImportRow> rows, IReadOnlyList<string> errors);

    ImportJob? Get(string jobId);

    void MarkCommitted(string jobId, int created, int updated);
}

public class InMemoryCatalogueJobStore : ICatalogueJobStore
{
    private readonly Dictionary<string, ImportJob> _jobs = new();
    private readonly object _lock = new();

    public string Create(IReadOnlyList<ProductImportRow> rows, IReadOnlyList<string> errors)
    {
        var id = Guid.NewGuid().ToString("N");
        lock (_lock)
        {
            _jobs[id] = new ImportJob { JobId = id, Rows = rows, Errors = errors, State = "Preview" };
        }
        return id;
    }

    public ImportJob? Get(string jobId)
    {
        lock (_lock)
        {
            return _jobs.TryGetValue(jobId, out var job) ? job : null;
        }
    }

    public void MarkCommitted(string jobId, int created, int updated)
    {
        lock (_lock)
        {
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.Created = created;
                job.Updated = updated;
            }
        }
    }
}