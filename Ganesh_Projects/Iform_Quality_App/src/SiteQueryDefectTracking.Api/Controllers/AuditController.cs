using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SiteQueryDefectTracking.Api.Common;
using SiteQueryDefectTracking.Application.Common;
using SiteQueryDefectTracking.Application.DTOs.Audit;
using SiteQueryDefectTracking.Application.DTOs.Queries;
using SiteQueryDefectTracking.Application.Interfaces;

namespace SiteQueryDefectTracking.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = AppPolicies.CanViewAuditLogs)]
public class AuditController(IAuditLogQueryService services) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> Search(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? action = null,
        [FromQuery] string? entityName = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? userId = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var result = await services.SearchAsync(new AuditLogSearchRequest
        {
            Page = page,
            PageSize = pageSize,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = userId,
            From = from,
            To = to
        }, ct);
        return Ok(ApiResponse.Ok(result));
    }
}