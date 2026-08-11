using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Products;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/products")]
[Authorize]
public class ProductsController(IProductService services, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductSummaryDto>>>> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? query = null,
        [FromQuery] string? category = null,
        [FromQuery] Guid? projectId = null,
        CancellationToken ct = default)
    {
        var result = await services.SearchAsync(new ProductSearchRequest
        {
            Page = page,
            PageSize = pageSize,
            Query = query,
            Category = category,
            ProjectId = projectId
        }, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Get([FromRoute] Guid id, CancellationToken ct)
    {
        var result = await services.GetAsync(id, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [Authorize(Policy = AppPolicies.CanManageCatalogue)]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Create([FromBody] CreateProductRequest request, CancellationToken ct)
    {
        var result = await services.CreateAsync(request, currentUser.IpAddress, currentUser.DeviceInfo, ct);
        return Ok(ApiResponse.Ok(result, "Product created."));
    }

    [Authorize(Policy = AppPolicies.CanManageCatalogue)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> Update([FromRoute] Guid id, [FromBody] UpdateProductRequest request, CancellationToken ct)
    {
        var result = await services.UpdateAsync(id, request, currentUser.IpAddress, currentUser.DeviceInfo, ct);
        return Ok(ApiResponse.Ok(result, "Product updated."));
    }

    [Authorize(Policy = AppPolicies.CanManageCatalogue)]
    [HttpPost("import/preview")]
    public async Task<ActionResult<ApiResponse<ProductImportSummary>>> ImportPreview([FromBody] IReadOnlyList<ProductImportRow> rows, CancellationToken ct)
    {
        var result = await services.ImportPreviewAsync(rows, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [Authorize(Policy = AppPolicies.CanManageCatalogue)]
    [HttpPost("import/{jobId}/commit")]
    public async Task<ActionResult<ApiResponse<ProductImportSummary>>> CommitImport([FromRoute] string jobId, CancellationToken ct)
    {
        var result = await services.CommitImportAsync(jobId, ct);
        return Ok(ApiResponse.Ok(result));
    }

    [Authorize(Policy = AppPolicies.CanManageCatalogue)]
    [HttpGet("import/{jobId}/status")]
    public async Task<ActionResult<ApiResponse<ImportStatusDto?>>> ImportStatus([FromRoute] string jobId, CancellationToken ct)
    {
        var result = await services.GetImportStatusAsync(jobId, ct);
        return Ok(ApiResponse.Ok(result));
    }
}