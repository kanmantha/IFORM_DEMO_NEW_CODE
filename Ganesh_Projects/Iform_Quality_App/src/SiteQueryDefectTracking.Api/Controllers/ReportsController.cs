using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.DTOs.Reports;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Policy = AppPolicies.CanViewDashboard)]
public class ReportsController(IReportService reports) : ControllerBase
{
    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<ReportResult>>> Generate([FromBody] ReportRequest request, CancellationToken ct)
    {
        var result = await reports.GenerateAsync(request, ct);
        return Ok(ApiResponse.Ok(result));
    }
}