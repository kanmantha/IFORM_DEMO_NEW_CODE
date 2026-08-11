using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.DTOs.Dashboard;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = AppPolicies.CanViewDashboard)]
public class DashboardController(IDashboardService services) : ControllerBase
{
    [HttpGet("snapshot")]
    public async Task<ActionResult<ApiResponse<DashboardSnapshotDto>>> Snapshot(CancellationToken ct)
    {
        var result = await services.GetSnapshotAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("open")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<QuerySummaryDto>>>> Open(CancellationToken ct)
    {
        var result = await services.GetOpenQueriesAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }
}