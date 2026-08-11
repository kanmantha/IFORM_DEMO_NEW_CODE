using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Shared;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController(IProjectService projects) : ControllerBase
{
    [HttpGet("active")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupItemDto>>>> Active(CancellationToken ct)
    {
        var result = await projects.GetActiveAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<LookupItemDto>>>> Search(
        [FromQuery] string? keyword = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await projects.SearchAsync(keyword, page, pageSize, ct);
        return Ok(ApiResponse.Ok(result));
    }
}