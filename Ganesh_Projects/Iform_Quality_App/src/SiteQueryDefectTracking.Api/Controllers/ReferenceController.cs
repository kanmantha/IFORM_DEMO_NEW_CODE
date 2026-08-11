using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.DTOs.Shared;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/reference")]
[Authorize]
public class ReferenceController(IReferenceService reference) : ControllerBase
{
    [HttpGet("issue-types")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<LookupItemDto>>>> IssueTypes(CancellationToken ct)
    {
        var result = await reference.GetIssueTypesAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("dispatch-statuses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<EnumOptionDto>>>> DispatchStatuses(CancellationToken ct)
    {
        var result = await reference.GetDispatchStatusesAsync(ct);
        return Ok(ApiResponse.Ok(result));
    }
}